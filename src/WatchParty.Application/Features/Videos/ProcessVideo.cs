using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

/// <summary>
/// Sent by the background worker after an upload: converts the file, extracts subtitles and marks the video
/// ready; then builds the adaptive-quality (HLS) version, which can take a while and is optional.
/// </summary>
public sealed record ProcessVideoCommand(Guid VideoId) : ICommand<Result>;

internal sealed class ProcessVideoCommandHandler(
    IAppDbContext db,
    IVideoProcessor processor,
    IVideoStorage storage,
    IVideoProgressTracker progress,
    TimeProvider time,
    ILogger<ProcessVideoCommandHandler> logger) : ICommandHandler<ProcessVideoCommand, Result>
{
    public async Task<Result> Handle(ProcessVideoCommand command, CancellationToken cancellationToken)
    {
        var video = await db.Videos.FirstOrDefaultAsync(v => v.Id == command.VideoId, cancellationToken);
        if (video is null) return VideoErrors.NotFound;

        if (video.Status == VideoStatus.Processing && !await ConvertAsync(video, cancellationToken)) return Result.Success();

        // HLS is built from the MP4 on disk, so finished files only go to remote storage afterwards.
        // Until then they are served from the server's disk.
        if (video.Status == VideoStatus.Ready && video.HlsKey is null && processor.HlsEnabled)
            await BuildHlsAsync(video, cancellationToken);

        if (video.Status == VideoStatus.Ready)
        {
            await OffloadAsync(() => storage.OffloadAsync(video.FileKey, video.ContentType, cancellationToken));
            if (video.ThumbnailKey is not null)
                await OffloadAsync(() => storage.OffloadAsync(video.ThumbnailKey, "image/jpeg", cancellationToken));
        }

        return Result.Success();
    }

    private async Task<bool> ConvertAsync(Video video, CancellationToken cancellationToken)
    {
        var sourceKey = video.FileKey;
        progress.Report(video.Id, 0);
        try
        {
            var processed = await processor.ProcessAsync(sourceKey, new Progress<double>(p => progress.Report(video.Id, p)), cancellationToken);
            video.MarkReady(processed.FileKey, processed.ContentType, processed.SizeBytes, processed.DurationSeconds, processed.ThumbnailKey);
            foreach (var sub in processed.Subtitles)
                db.Subtitles.Add(Subtitle.Create(video.Id, sub.Label, sub.Language, sub.FileKey, null, time.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync(cancellationToken);

            if (processed.FileKey != sourceKey) storage.Delete(sourceKey);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to process video {VideoId}", video.Id);
            video.MarkFailed("Não foi possível converter esse vídeo.");
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
        finally
        {
            progress.Clear(video.Id);
        }
    }

    /// <summary>Moving files to remote storage is best effort: if it fails they are simply served from disk.</summary>
    private async Task OffloadAsync(Func<Task> offload)
    {
        try
        {
            await offload();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not move a file to remote storage; it stays on the server");
        }
    }

    private async Task BuildHlsAsync(Video video, CancellationToken cancellationToken)
    {
        // Already moved to remote storage (e.g. HLS was enabled later): not worth downloading it back.
        if (storage.Locate(video.FileKey) is not { Path: not null }) return;
        try
        {
            var hlsKey = await processor.GenerateHlsAsync(video.FileKey, cancellationToken);
            if (hlsKey is null) return;

            // The video may have been deleted while we were encoding.
            if (!await db.Videos.AnyAsync(v => v.Id == video.Id, cancellationToken))
            {
                storage.DeleteFolder(hlsKey);
                return;
            }
            video.SetHls(hlsKey);
            await db.SaveChangesAsync(cancellationToken);
            await OffloadAsync(() => storage.OffloadFolderAsync(hlsKey, f => !f.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase), cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The MP4 keeps working; HLS is a bonus.
            logger.LogWarning(ex, "Could not build HLS for video {VideoId}", video.Id);
        }
    }
}
