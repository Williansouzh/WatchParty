using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WatchParty.Application;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Users;
using WatchParty.Domain.Videos;
using WatchParty.Infrastructure.Persistence;
using WatchParty.Infrastructure.Media;
using WatchParty.Infrastructure.Realtime;
using WatchParty.Infrastructure.Storage;
using WatchParty.Infrastructure.Security;

namespace WatchParty.Tests.Features;

/// <summary>Application layer wired to a real (in-memory) SQLite database with the real migrations.</summary>
public sealed class TestApp : IAsyncDisposable
{
    public const string InviteCode = "convite-teste";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _provider;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 1, 20, 0, 0, TimeSpan.Zero));
    public IRoomPresence Presence { get; }
    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "watchparty-tests", Guid.NewGuid().ToString("N"));
    public RecordingQueue Queue { get; } = new();

    public TestApp(Action<IServiceCollection>? configure = null, Dictionary<string, string?>? settings = null)
    {
        Presence = new InMemoryRoomPresence(Time);
        _connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:InviteCode"] = InviteCode })
            .AddInMemoryCollection(settings ?? [])
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(configuration);
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton(Presence);
        services.AddSingleton<IVideoProgressTracker, VideoProgressTracker>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new StorageOptions { RootPath = MediaRoot }));
        services.AddSingleton<IVideoStorage, LocalVideoStorage>();
        services.AddSingleton<IVideoProcessingQueue>(Queue);
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }

    public async Task<T> Send<T>(IRequest<T> request)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<T> Db<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<Guid> SeedUserAsync(string userName = "will") => Db(async db =>
    {
        var user = User.Create(userName, userName, "hash", Time.GetUtcNow().UtcDateTime);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    });

    public Task<Guid> SeedVideoAsync(Guid uploaderId, bool ready = true) => Db(async db =>
    {
        var video = Video.Create("Filme", null, "filme.mp4", "key.mp4", "video/mp4", 100, uploaderId, Time.GetUtcNow().UtcDateTime);
        if (ready) video.MarkReady("key.mp4", "video/mp4", 100, 5400, null);
        db.Videos.Add(video);
        await db.SaveChangesAsync();
        return video.Id;
    });

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
        if (Directory.Exists(MediaRoot)) Directory.Delete(MediaRoot, recursive: true);
    }

    public sealed class RecordingQueue : IVideoProcessingQueue
    {
        public List<Guid> Enqueued { get; } = [];
        public void Enqueue(Guid videoId) => Enqueued.Add(videoId);
    }
}
