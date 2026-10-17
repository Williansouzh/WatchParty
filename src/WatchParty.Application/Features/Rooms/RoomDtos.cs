using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Features.Rooms;

/// <summary>Playback position at <see cref="UpdatedAtMs"/> (Unix ms, server clock).</summary>
public sealed record PlaybackStateDto(Guid? VideoId, bool IsPlaying, double PositionSeconds, long UpdatedAtMs)
{
    public static PlaybackStateDto From(Room room) => new(
        room.CurrentVideoId,
        room.IsPlaying,
        room.PositionSeconds,
        new DateTimeOffset(DateTime.SpecifyKind(room.StateUpdatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds());
}

public sealed record RoomDto(
    Guid Id,
    string Code,
    string Name,
    Guid HostId,
    string HostName,
    VideoDto? CurrentVideo,
    PlaybackStateDto Playback,
    int OnlineCount,
    DateTime CreatedAt);

public sealed record RoomSummaryDto(
    string Code,
    string Name,
    string HostName,
    Guid? CurrentVideoId,
    string? CurrentVideoTitle,
    bool HasThumbnail,
    int OnlineCount,
    DateTime CreatedAt);

public sealed record RoomVideoChangedDto(VideoDto Video, PlaybackStateDto Playback);
