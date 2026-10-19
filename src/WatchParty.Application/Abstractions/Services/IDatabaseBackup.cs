namespace WatchParty.Application.Abstractions.Services;

public interface IDatabaseBackup
{
    /// <summary>Writes a consistent copy of the database and prunes old copies. Returns the file written.</summary>
    Task<string> BackupAsync(CancellationToken cancellationToken);
}
