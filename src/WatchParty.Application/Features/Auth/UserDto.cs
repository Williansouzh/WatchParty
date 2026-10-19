using WatchParty.Domain.Users;

namespace WatchParty.Application.Features.Auth;

public sealed record UserDto(Guid Id, string UserName, string DisplayName, string AvatarColor)
{
    public static UserDto From(User user) => new(user.Id, user.UserName, user.DisplayName, user.AvatarColor);
}

/// <summary>A successful login: who it is and the stamp that keeps the login valid until it changes.</summary>
public sealed record AuthSession(UserDto User, string SecurityStamp)
{
    public static AuthSession From(User user) => new(UserDto.From(user), user.SecurityStamp);
}
