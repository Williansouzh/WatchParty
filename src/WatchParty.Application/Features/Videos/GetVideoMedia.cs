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

/// <summary>A file to send: from disk (<see cref="PhysicalPath"/>), as a redirect (<see cref="RedirectUrl"/>) or as text.</summary>
public sealed record MediaFileDto(string? PhysicalPath, string ContentType, string? RedirectUrl = null, string? Content = null)
{
    public static MediaFileDto From(MediaLocation location, string contentType) =>
        new(location.Path, contentType, location.Url);
}

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
            return video.ThumbnailKey is not null && storage.Locate(video.ThumbnailKey) is { } thumbnail
                ? MediaFileDto.From(thumbnail, "image/jpeg")
                : VideoErrors.NoThumbnail;
        }

        if (video.Status != VideoStatus.Ready) return VideoErrors.NotReady;
        return storage.Locate(video.FileKey) is { } file ? MediaFileDto.From(file, video.ContentType) : VideoErrors.NotFound;
    }
}

/// <summary>A file of the HLS version (playlist or segment), by its path inside the video's HLS folder.</summary>
public sealed record GetHlsFileQuery(Guid VideoId, string Path) : IQuery<Result<MediaFileDto>>;

internal sealed class GetHlsFileQueryHandler(IAppDbContext db, IVideoStorage storage) : IQueryHandler<GetHlsFileQuery, Result<MediaFileDto>>
{
    public async Task<Result<MediaFileDto>> Handle(GetHlsFileQuery query, CancellationToken cancellationToken)
    {
        var hlsKey = await db.Videos.AsNoTracking().Where(v => v.Id == query.VideoId).Select(v => v.HlsKey).FirstOrDefaultAsync(cancellationToken);
        if (hlsKey is null) return VideoErrors.NotFound;

        var contentType = System.IO.Path.GetExtension(query.Path).ToLowerInvariant() switch
        {
            ".m3u8" => "application/vnd.apple.mpegurl",
            ".ts" => "video/mp2t",
            ".m4s" => "video/iso.segment",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };

        // Playlists always stay on the server. When the segments were moved to remote storage, the playlist
        // is rewritten so the player fetches each segment straight from there with a signed URL.
        if (query.Path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            var playlist = storage.ResolveFolderFile(hlsKey, query.Path);
            if (playlist is null) return VideoErrors.NotFound;

            var directory = System.IO.Path.GetDirectoryName(query.Path)?.Replace('\\', '/') ?? "";
            var lines = await File.ReadAllLinesAsync(playlist, cancellationToken);
            var rewritten = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) continue;

                var segment = directory.Length > 0 ? $"{directory}/{line}" : line;
                if (storage.LocateInFolder(hlsKey, segment) is { Url: { } url })
                {
                    lines[i] = url;
                    rewritten = true;
                }
            }
            return rewritten
                ? new MediaFileDto(null, contentType, Content: string.Join('\n', lines) + "\n")
                : new MediaFileDto(playlist, contentType);
        }

        return storage.LocateInFolder(hlsKey, query.Path) is { } location
            ? MediaFileDto.From(location, contentType)
            : VideoErrors.NotFound;
    }
}
