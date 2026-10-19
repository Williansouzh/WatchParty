namespace WatchParty.Infrastructure.Storage;

/// <summary>
/// Optional remote storage for finished videos (section "Storage:S3"). Works with Amazon S3, Cloudflare R2,
/// Backblaze B2, MinIO… Uploads and conversion still happen on the server's disk; the results are then moved
/// to the bucket and players download them directly with short-lived signed URLs.
/// </summary>
public sealed class S3Options
{
    public const string SectionName = "Storage:S3";

    public bool Enabled { get; init; }

    /// <summary>Endpoint for non-AWS providers, e.g. https://&lt;account&gt;.r2.cloudflarestorage.com. Empty for AWS.</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>"auto" for R2, the bucket region for AWS (e.g. sa-east-1).</summary>
    public string Region { get; init; } = "auto";

    public string Bucket { get; init; } = "";
    public string AccessKey { get; init; } = "";
    public string SecretKey { get; init; } = "";

    /// <summary>Needed by MinIO and most self-hosted providers.</summary>
    public bool ForcePathStyle { get; init; } = true;

    public string KeyPrefix { get; init; } = "media/";
    public int UrlExpiryMinutes { get; init; } = 360;
}
