using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, ISender sender, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new RegisterUserCommand(request.UserName, request.DisplayName, request.Password, request.InviteCode), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            await SignInAsync(http, result.Value);
            return Results.Ok(result.Value);
        }).RequireRateLimiting(RateLimits.Auth);

        group.MapPost("/login", async (LoginRequest request, ISender sender, HttpContext http, CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(request.UserName, request.Password), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            await SignInAsync(http, result.Value);
            return Results.Ok(result.Value);
        }).RequireRateLimiting(RateLimits.Auth);

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

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

    private static Task SignInAsync(HttpContext http, UserDto user)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimsPrincipalExtensions.DisplayNameClaim, user.DisplayName),
            new(ClaimsPrincipalExtensions.AvatarColorClaim, user.AvatarColor)
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });
    }
}
