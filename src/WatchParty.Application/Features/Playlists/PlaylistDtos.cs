using WatchParty.Application.Features.Videos;

namespace WatchParty.Application.Features.Playlists;

public sealed record PlaylistSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    int ItemCount,
    Guid? CoverVideoId,
    DateTime CreatedAt);

public sealed record PlaylistDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    DateTime CreatedAt,
    IReadOnlyList<PlaylistItemDto> Items);

public sealed record PlaylistItemDto(Guid ItemId, DateTime AddedAt, VideoDto Video);
