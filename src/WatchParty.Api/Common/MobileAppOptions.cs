namespace WatchParty.Api.Common;

/// <summary>Section "MobileApp": the newest Android build, so installed apps can offer the update.</summary>
public sealed class MobileAppOptions
{
    public const string SectionName = "MobileApp";

    /// <summary>Build number (the "+N" in pubspec version). 0 = no update announced.</summary>
    public int LatestBuild { get; init; }
    public string? LatestVersion { get; init; }

    /// <summary>Where the server finds the APK to serve at /download/watchparty.apk.</summary>
    public string? ApkPath { get; init; } = "data/app/WatchParty.apk";

    /// <summary>Optional external link instead of serving the file.</summary>
    public string? DownloadUrl { get; init; }
}
