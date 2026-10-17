using WatchParty.Domain.Common;

namespace WatchParty.Domain.Users;

public sealed class User : Entity
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 32;
    public const int DisplayNameMaxLength = 50;

    private static readonly string[] AvatarColors =
        ["#1DB954", "#E91429", "#8D67AB", "#E8115B", "#1E3264", "#BA5D07", "#148A08", "#509BF5", "#AF2896", "#DC148C"];

    private User() { }

    public string UserName { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string AvatarColor { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    public static User Create(string userName, string displayName, string passwordHash, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new DomainException("O usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("O nome é obrigatório.");

        return new User
        {
            UserName = NormalizeUserName(userName),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            AvatarColor = AvatarColors[Random.Shared.Next(AvatarColors.Length)],
            CreatedAt = now
        };
    }

    public static string NormalizeUserName(string userName) => userName.Trim().ToLowerInvariant();
}
