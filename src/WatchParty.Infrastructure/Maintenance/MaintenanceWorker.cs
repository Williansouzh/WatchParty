using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Maintenance;

namespace WatchParty.Infrastructure.Maintenance;

/// <summary>Hourly: cleans up idle rooms/old chat. Every <see cref="BackupOptions.IntervalHours"/>: backs up the database.</summary>
internal sealed class MaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BackupOptions> backupOptions,
    TimeProvider time,
    ILogger<MaintenanceWorker> logger) : BackgroundService
{
    private DateTimeOffset? _lastBackup;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), time, stoppingToken); // let startup finish first
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CleanupCommand(), cancellationToken);
            if (result.RoomsDeleted + result.MessagesDeleted > 0)
                logger.LogInformation("Cleanup removed {Rooms} idle rooms and {Messages} old messages", result.RoomsDeleted, result.MessagesDeleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Cleanup failed");
        }

        var backup = backupOptions.Value;
        if (!backup.Enabled || (_lastBackup is { } last && time.GetUtcNow() - last < TimeSpan.FromHours(backup.IntervalHours))) return;

        try
        {
            var file = await scope.ServiceProvider.GetRequiredService<IDatabaseBackup>().BackupAsync(cancellationToken);
            _lastBackup = time.GetUtcNow();
            logger.LogInformation("Database backed up to {File}", file);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Database backup failed");
        }
    }
}
