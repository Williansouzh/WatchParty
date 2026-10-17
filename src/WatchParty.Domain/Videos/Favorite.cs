namespace WatchParty.Domain.Videos;

/// <summary>A video a user "liked" (the Spotify-like "Curtidos" collection).</summary>
public sealed class Favorite
{
    private Favorite() { }

    public Guid UserId { get; private set; }
    public Guid VideoId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Favorite Create(Guid userId, Guid videoId, DateTime now) =>
        new() { UserId = userId, VideoId = videoId, CreatedAt = now };
}
