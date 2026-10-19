using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using WatchParty.Api.Extensions;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Auth;

namespace WatchParty.Api.Common;

/// <summary>
/// Cookies and bearer tokens are self-contained, so they can't be "deleted". Instead every login carries the
/// user's security stamp; when the stamp changes (password change, sign out everywhere) old logins stop working.
/// The current stamp is cached briefly to avoid a database hit per request.
/// </summary>
public sealed class SecurityStampValidator(IMemoryCache cache)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public async Task<bool> IsValidAsync(ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var stamp = user.FindFirstValue(ClaimsPrincipalExtensions.SecurityStampClaim);
        if (stamp is null) return false; // logins issued before stamps existed

        var userId = user.GetUserId();
        var current = await cache.GetOrCreateAsync(CacheKey(userId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return sender.Send(new GetSecurityStampQuery(userId), cancellationToken);
        });
        return current is not null && current == stamp;
    }

    public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));

    private static string CacheKey(Guid userId) => $"stamp:{userId}";
}

public static class SecurityStampMiddleware
{
    /// <summary>Rejects authenticated requests whose login was revoked. Must run after UseAuthentication.</summary>
    public static IApplicationBuilder UseSecurityStampValidation(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var validator = context.RequestServices.GetRequiredService<SecurityStampValidator>();
            var sender = context.RequestServices.GetRequiredService<ISender>();
            if (!await validator.IsValidAsync(context.User, sender, context.RequestAborted))
            {
                if (context.Request.Cookies.ContainsKey("watchparty.auth"))
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }
        await next(context);
    });
}
