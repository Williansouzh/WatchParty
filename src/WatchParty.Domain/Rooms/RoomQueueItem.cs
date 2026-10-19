using WatchParty.Domain.Common;
using WatchParty.Domain.Users;
using WatchParty.Domain.Videos;

namespace WatchParty.Domain.Rooms;

/// <summary>A video waiting to be played in a room ("up next").</summary>
public sealed class RoomQueueItem : Entity
{
    private RoomQueueItem() { }

    public Guid RoomId { get; private set; }
    public Guid VideoId { get; private set; }
    public Video Video { get; private set; } = null!;
    public int Position { get; private set; }
    public Guid AddedById { get; private set; }
    public User AddedBy { get; private set; } = null!;
    public DateTime AddedAt { get; private set; }

    public static RoomQueueItem Create(Guid roomId, Guid videoId, int position, Guid addedById, DateTime now) =>
        new() { RoomId = roomId, VideoId = videoId, Position = position, AddedById = addedById, AddedAt = now };
}
