using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WatchParty.Api.Common;
using WatchParty.Api.Endpoints;
using WatchParty.Api.Hubs;
using WatchParty.Application;
using WatchParty.Infrastructure;
using WatchParty.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.Configure<WebRtcOptions>(builder.Configuration.GetSection(WebRtcOptions.SectionName));

// Browser uses a cookie; the mobile app sends "Authorization: Bearer <token>"
// (or ?access_token= on the SignalR websocket, where headers can't be set).
const string AuthScheme = "CookieOrToken";
builder.Services.AddAuthentication(AuthScheme)
    .AddPolicyScheme(AuthScheme, AuthScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || context.Request.Query.ContainsKey("access_token")
                ? BearerTokenDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "watchparty.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // It's an API + SPA: answer with status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddBearerToken(options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromDays(30);
        options.Events.OnMessageReceived = context =>
        {
            if (context.Request.Query.TryGetValue("access_token", out var token)
                && (context.Request.Path.StartsWithSegments("/hubs") || context.Request.Path.StartsWithSegments("/api/videos")))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimits.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR(options => options.AddFilter<HubExceptionFilter>())
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Keys that sign cookies and tokens live next to the data, so logins survive restarts and container rebuilds.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "data/keys"));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (app.Services.GetRequiredService<IOptions<AuthOptions>>().Value.InviteCode is "" or "troque-este-codigo")
    app.Logger.LogWarning("Defina um código de convite próprio em Auth:InviteCode antes de chamar seus amigos.");

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapVideoEndpoints();
app.MapPlaylistEndpoints();
app.MapRoomEndpoints();
app.MapHub<RoomHub>("/hubs/room");

app.Run();

public partial class Program;
