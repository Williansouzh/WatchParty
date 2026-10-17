using System.Linq.Expressions;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public sealed record VideoDto(
    Guid Id,
    string Title,
    string? Description,
    VideoStatus Status,
    string? FailureReason,
    double? DurationSeconds,
    long SizeBytes,
    bool HasThumbnail,
    Guid UploadedById,
    string UploadedByName,
    DateTime UploadedAt,
    bool IsFavorite);

internal static class VideoMappings
{
    public static Expression<Func<Video, VideoDto>> ToDto(IQueryable<Favorite> favorites, Guid userId) =>
        v => new VideoDto(
            v.Id,
            v.Title,
            v.Description,
            v.Status,
            v.FailureReason,
            v.DurationSeconds,
            v.SizeBytes,
            v.ThumbnailKey != null,
            v.UploadedById,
            v.UploadedBy.DisplayName,
            v.UploadedAt,
            favorites.Any(f => f.UserId == userId && f.VideoId == v.Id));
}
