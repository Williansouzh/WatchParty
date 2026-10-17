using WatchParty.Domain.Users;

namespace WatchParty.Application.Features.Auth;

public sealed record UserDto(Guid Id, string UserName, string DisplayName, string AvatarColor)
{
    public static UserDto From(User user) => new(user.Id, user.UserName, user.DisplayName, user.AvatarColor);
}
