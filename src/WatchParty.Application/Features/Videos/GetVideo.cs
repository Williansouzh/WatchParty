using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public sealed record GetVideoQuery(Guid VideoId, Guid UserId) : IQuery<Result<VideoDto>>;

internal sealed class GetVideoQueryHandler(IAppDbContext db, IVideoProgressTracker progress) : IQueryHandler<GetVideoQuery, Result<VideoDto>>
{
    public async Task<Result<VideoDto>> Handle(GetVideoQuery query, CancellationToken cancellationToken)
    {
        var video = await LoadAsync(db, query.VideoId, query.UserId, cancellationToken);
        return video is null ? VideoErrors.NotFound : video.WithProgress(progress);
    }

    /// <summary>A single video with its subtitle tracks (shared with the room queries).</summary>
    internal static async Task<VideoDto?> LoadAsync(IAppDbContext db, Guid videoId, Guid userId, CancellationToken cancellationToken)
    {
        var video = await db.Videos.AsNoTracking()
            .Where(v => v.Id == videoId)
            .Select(VideoMappings.ToDto(db.Favorites, userId))
            .FirstOrDefaultAsync(cancellationToken);
        if (video is null) return null;

        var subtitles = await db.Subtitles.AsNoTracking()
            .Where(s => s.VideoId == videoId)
            .OrderBy(s => s.CreatedAt)
            .Select(VideoMappings.ToSubtitleDto)
            .ToListAsync(cancellationToken);

        return video with { Subtitles = subtitles };
    }
}
