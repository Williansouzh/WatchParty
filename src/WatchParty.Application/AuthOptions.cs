namespace WatchParty.Application;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Code your friends need to create an account. Keeps strangers out.</summary>
    public string InviteCode { get; init; } = string.Empty;
}
