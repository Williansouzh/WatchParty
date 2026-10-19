using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
using WatchParty.Api.Common;
using WatchParty.Api.Extensions;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Auth;

namespace WatchParty.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record RegisterRequest(string UserName, string DisplayName, string Password, string InviteCode);
    public sealed record LoginRequest(string UserName, string Password);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // ----- Browser: cookie -----

        group.MapPost("/register", async (RegisterRequest request, ISender sender, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new RegisterUserCommand(request.UserName, request.DisplayName, request.Password, request.InviteCode), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            await SignInWithCookieAsync(http, result.Value);
            return Results.Ok(result.Value.User);
        }).RequireRateLimiting(RateLimits.Auth);

        group.MapPost("/login", async (LoginRequest request, ISender sender, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(request.UserName, request.Password), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            await SignInWithCookieAsync(http, result.Value);
            return Results.Ok(result.Value.User);
        }).RequireRateLimiting(RateLimits.Auth);

        // ----- Mobile app: same credentials, answers with a bearer token -----

        group.MapPost("/token/register", async (RegisterRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new RegisterUserCommand(request.UserName, request.DisplayName, request.Password, request.InviteCode), ct);
            return result.IsSuccess ? IssueToken(result.Value) : result.Error.ToProblem();
        }).RequireRateLimiting(RateLimits.Auth);

        group.MapPost("/token/login", async (LoginRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(request.UserName, request.Password), ct);
            return result.IsSuccess ? IssueToken(result.Value) : result.Error.ToProblem();
        }).RequireRateLimiting(RateLimits.Auth);

        // ----- Session management -----

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        // Ends every login of this account on every device (cookies and tokens).
        group.MapPost("/logout-everywhere", async (ClaimsPrincipal user, ISender sender, SecurityStampValidator stamps, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(new SignOutEverywhereCommand(user.GetUserId()), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            stamps.Invalidate(user.GetUserId());
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        // Other devices are signed out; this one gets a fresh login (cookie or token, like the request).
        group.MapPost("/password", async (ChangePasswordRequest request, ClaimsPrincipal user, ISender sender,
            SecurityStampValidator stamps, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(new ChangePasswordCommand(user.GetUserId(), request.CurrentPassword, request.NewPassword), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            stamps.Invalidate(user.GetUserId());
            if (IsBearerRequest(http)) return IssueToken(result.Value);

            await SignInWithCookieAsync(http, result.Value);
            return Results.Ok(result.Value.User);
        }).RequireAuthorization().RequireRateLimiting(RateLimits.Auth);

        group.MapGet("/me", async (ClaimsPrincipal user, ISender sender, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUserQuery(user.GetUserId()), ct);
            if (result.IsSuccess) return Results.Ok(result.Value);

            // The account no longer exists (e.g. database was reset).
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Unauthorized();
        }).RequireAuthorization();

        return app;
    }

    private static bool IsBearerRequest(HttpContext http) =>
        http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    private static Task SignInWithCookieAsync(HttpContext http, AuthSession session) =>
        http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal(session, CookieAuthenticationDefaults.AuthenticationScheme),
            new AuthenticationProperties { IsPersistent = true });

    /// <summary>Writes { tokenType, accessToken, expiresIn, refreshToken }.</summary>
    private static IResult IssueToken(AuthSession session) =>
        Results.SignIn(CreatePrincipal(session, BearerTokenDefaults.AuthenticationScheme), authenticationScheme: BearerTokenDefaults.AuthenticationScheme);

    private static ClaimsPrincipal CreatePrincipal(AuthSession session, string authenticationType)
    {
        var user = session.User;
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimsPrincipalExtensions.DisplayNameClaim, user.DisplayName),
            new(ClaimsPrincipalExtensions.AvatarColorClaim, user.AvatarColor),
            new(ClaimsPrincipalExtensions.SecurityStampClaim, session.SecurityStamp)
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }
}
