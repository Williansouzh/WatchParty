using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Infrastructure.Maintenance;
using WatchParty.Infrastructure.Media;
using WatchParty.Infrastructure.Persistence;
using WatchParty.Infrastructure.Realtime;
using WatchParty.Infrastructure.Security;
using WatchParty.Infrastructure.Storage;

namespace WatchParty.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=data/watchparty.db";
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IRoomPresence, InMemoryRoomPresence>();
        services.AddSingleton<IVideoProgressTracker, VideoProgressTracker>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
        services.AddSingleton<LocalVideoStorage>();
        if (configuration.GetValue<bool>($"{S3Options.SectionName}:Enabled"))
            services.AddSingleton<IVideoStorage, S3VideoStorage>();
        else
            services.AddSingleton<IVideoStorage>(sp => sp.GetRequiredService<LocalVideoStorage>());

        services.Configure<FfmpegOptions>(configuration.GetSection(FfmpegOptions.SectionName));
        services.AddScoped<IVideoProcessor, FfmpegVideoProcessor>();
        services.AddSingleton<VideoProcessingQueue>();
        services.AddSingleton<IVideoProcessingQueue>(sp => sp.GetRequiredService<VideoProcessingQueue>());
        services.AddHostedService<VideoProcessingWorker>();

        services.Configure<BackupOptions>(configuration.GetSection(BackupOptions.SectionName));
        services.AddScoped<IDatabaseBackup, SqliteDatabaseBackup>();
        services.AddHostedService<MaintenanceWorker>();

        return services;
    }
}
