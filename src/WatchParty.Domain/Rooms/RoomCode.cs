using System.Security.Cryptography;

namespace WatchParty.Domain.Rooms;

public static class RoomCode
{
    public const int Length = 6;

    // No 0/O, 1/I to keep codes easy to read out loud.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
