using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Domain.Playlists;

public sealed class PlaylistItem : Entity
{
    private PlaylistItem() { }

    public Guid PlaylistId { get; private set; }
    public Guid VideoId { get; private set; }
    public Video Video { get; private set; } = null!;
    public int Position { get; private set; }
    public DateTime AddedAt { get; private set; }

    internal static PlaylistItem Create(Guid playlistId, Guid videoId, int position, DateTime now) =>
        new() { PlaylistId = playlistId, VideoId = videoId, Position = position, AddedAt = now };
}
