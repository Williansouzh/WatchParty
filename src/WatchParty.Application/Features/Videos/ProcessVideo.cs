using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

/// <summary>Sent by the background worker after an upload: converts the file and marks the video ready.</summary>
public sealed record ProcessVideoCommand(Guid VideoId) : ICommand<Result>;

internal sealed class ProcessVideoCommandHandler(
    IAppDbContext db,
    IVideoProcessor processor,
    IVideoStorage storage,
    ILogger<ProcessVideoCommandHandler> logger) : ICommandHandler<ProcessVideoCommand, Result>
{
    public async Task<Result> Handle(ProcessVideoCommand command, CancellationToken cancellationToken)
    {
        var video = await db.Videos.FirstOrDefaultAsync(v => v.Id == command.VideoId, cancellationToken);
        if (video is null) return VideoErrors.NotFound;
        if (video.Status != VideoStatus.Processing) return Result.Success();

        var sourceKey = video.FileKey;
        try
        {
            var processed = await processor.ProcessAsync(sourceKey, cancellationToken);
            video.MarkReady(processed.FileKey, processed.ContentType, processed.SizeBytes, processed.DurationSeconds, processed.ThumbnailKey);
            await db.SaveChangesAsync(cancellationToken);

            if (processed.FileKey != sourceKey) storage.Delete(sourceKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to process video {VideoId}", video.Id);
            video.MarkFailed("Não foi possível converter esse vídeo.");
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
