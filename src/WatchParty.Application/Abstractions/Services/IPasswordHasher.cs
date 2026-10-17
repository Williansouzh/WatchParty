namespace WatchParty.Application.Abstractions.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}
