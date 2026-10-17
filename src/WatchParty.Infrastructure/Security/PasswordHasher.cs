using Microsoft.AspNetCore.Identity;
using IPasswordHasher = WatchParty.Application.Abstractions.Services.IPasswordHasher;

namespace WatchParty.Infrastructure.Security;

/// <summary>PBKDF2 hashing from ASP.NET Core Identity, without the rest of Identity.</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private static readonly object User = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(User, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(User, passwordHash, password) != PasswordVerificationResult.Failed;
}
