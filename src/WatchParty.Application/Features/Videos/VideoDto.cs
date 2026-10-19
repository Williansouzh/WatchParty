using System.Linq.Expressions;
using WatchParty.Application.Abstractions.Services;
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
    bool IsFavorite,
    bool HasHls)
{
    /// <summary>0..1 while the server converts the file.</summary>
    public double? ProcessingProgress { get; init; }

    /// <summary>Only filled where a single video is shown (video page, room).</summary>
    public IReadOnlyList<SubtitleDto> Subtitles { get; init; } = [];
}

public sealed record SubtitleDto(Guid Id, string Label, string? Language, Guid? UploadedById);

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
            favorites.Any(f => f.UserId == userId && f.VideoId == v.Id),
            v.HlsKey != null);

    public static Expression<Func<Subtitle, SubtitleDto>> ToSubtitleDto =>
        s => new SubtitleDto(s.Id, s.Label, s.Language, s.UploadedById);

    public static VideoDto WithProgress(this VideoDto video, IVideoProgressTracker tracker) =>
        video.Status == VideoStatus.Processing ? video with { ProcessingProgress = tracker.Get(video.Id) } : video;
}
