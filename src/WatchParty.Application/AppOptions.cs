namespace WatchParty.Application;

/// <summary>Upload limits. Section "Storage".</summary>
public sealed class StorageLimitsOptions
{
    public const string SectionName = "Storage";

    /// <summary>Largest single file accepted. 0 = no limit.</summary>
    public long MaxUploadBytes { get; init; } = 20L * 1024 * 1024 * 1024;

    /// <summary>Total space each person can use. 0 = no limit.</summary>
    public long UserQuotaBytes { get; init; }

    /// <summary>Uploads are refused when the disk would be left with less than this.</summary>
    public long MinFreeBytes { get; init; } = 2L * 1024 * 1024 * 1024;
}

/// <summary>Housekeeping. Section "Maintenance".</summary>
public sealed class MaintenanceOptions
{
    public const string SectionName = "Maintenance";

    /// <summary>Rooms with nobody online and no activity for this long are deleted. 0 = keep forever.</summary>
    public int RoomInactiveDays { get; init; } = 30;

    /// <summary>Chat messages older than this are deleted. 0 = keep forever.</summary>
    public int ChatRetentionDays { get; init; } = 180;
}
