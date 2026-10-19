using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Infrastructure.Persistence;

namespace WatchParty.Infrastructure.Maintenance;

/// <summary>Uses SQLite's VACUUM INTO: a consistent, compact copy even while the app is writing.</summary>
internal sealed class SqliteDatabaseBackup(AppDbContext db, IOptions<BackupOptions> options, TimeProvider time) : IDatabaseBackup
{
    public async Task<string> BackupAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var directory = Path.GetFullPath(settings.Directory);
        Directory.CreateDirectory(directory);

        var file = Path.Combine(directory, $"watchparty-{time.GetUtcNow():yyyyMMdd-HHmmss}.db");
        var escaped = file.Replace("'", "''");
#pragma warning disable EF1002 // the path is ours, not user input
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", cancellationToken);
#pragma warning restore EF1002

        foreach (var old in new DirectoryInfo(directory).GetFiles("watchparty-*.db")
                     .OrderByDescending(f => f.Name)
                     .Skip(Math.Max(1, settings.Keep)))
        {
            old.Delete();
        }
        return file;
    }
}
