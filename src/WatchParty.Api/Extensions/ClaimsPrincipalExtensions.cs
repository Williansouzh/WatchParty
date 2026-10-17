using System.Security.Claims;

namespace WatchParty.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public const string DisplayNameClaim = "display_name";
    public const string AvatarColorClaim = "avatar_color";

    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("Authenticated user has no id claim.");

    public static string GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(DisplayNameClaim) ?? user.Identity?.Name ?? "?";

    public static string GetAvatarColor(this ClaimsPrincipal user) =>
        user.FindFirstValue(AvatarColorClaim) ?? "#1DB954";
}
