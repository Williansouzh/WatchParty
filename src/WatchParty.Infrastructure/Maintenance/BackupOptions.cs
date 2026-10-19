namespace WatchParty.Infrastructure.Maintenance;

/// <summary>Automatic copies of the database (not the videos). Section "Backup".</summary>
public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    public bool Enabled { get; init; } = true;
    public string Directory { get; init; } = "data/backups";
    public int IntervalHours { get; init; } = 24;
    public int Keep { get; init; } = 7;
}
