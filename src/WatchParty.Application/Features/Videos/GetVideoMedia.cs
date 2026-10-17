using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public enum MediaKind
{
    Video,
    Thumbnail
}

public sealed record MediaFileDto(string PhysicalPath, string ContentType);

public sealed record GetVideoMediaQuery(Guid VideoId, MediaKind Kind) : IQuery<Result<MediaFileDto>>;

internal sealed class GetVideoMediaQueryHandler(IAppDbContext db, IVideoStorage storage)
    : IQueryHandler<GetVideoMediaQuery, Result<MediaFileDto>>
{
    public async Task<Result<MediaFileDto>> Handle(GetVideoMediaQuery query, CancellationToken cancellationToken)
    {
        var video = await db.Videos.AsNoTracking()
            .Where(v => v.Id == query.VideoId)
            .Select(v => new { v.Status, v.FileKey, v.ContentType, v.ThumbnailKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (video is null) return VideoErrors.NotFound;

        if (query.Kind == MediaKind.Thumbnail)
        {
            return video.ThumbnailKey is null || !storage.Exists(video.ThumbnailKey)
                ? VideoErrors.NoThumbnail
                : new MediaFileDto(storage.GetPhysicalPath(video.ThumbnailKey), "image/jpeg");
        }

        if (video.Status != VideoStatus.Ready) return VideoErrors.NotReady;
        if (!storage.Exists(video.FileKey)) return VideoErrors.NotFound;

        return new MediaFileDto(storage.GetPhysicalPath(video.FileKey), video.ContentType);
    }
}
