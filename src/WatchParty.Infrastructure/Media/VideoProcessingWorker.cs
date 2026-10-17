using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Videos;
using WatchParty.Infrastructure.Persistence;

namespace WatchParty.Infrastructure.Media;

/// <summary>Processes uploads one at a time so a long movie conversion doesn't starve the server.</summary>
internal sealed class VideoProcessingWorker(
    VideoProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<VideoProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueInterruptedAsync(stoppingToken);

        await foreach (var videoId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new ProcessVideoCommand(videoId), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error processing video {VideoId}", videoId);
            }
        }
    }

    /// <summary>Videos left in "Processing" by a restart are picked up again.</summary>
    private async Task RequeueInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.Videos.Where(v => v.Status == VideoStatus.Processing).Select(v => v.Id).ToListAsync(cancellationToken);
        foreach (var id in pending) queue.Enqueue(id);
    }
}
