namespace WatchParty.Application.Abstractions.Services;

/// <summary>Converts an uploaded file into something every browser can play and extracts metadata.</summary>
public interface IVideoProcessor
{
    /// <param name="progress">Receives 0..1 while converting.</param>
    Task<ProcessedVideo> ProcessAsync(string sourceKey, IProgress<double>? progress, CancellationToken cancellationToken);

    bool HlsEnabled { get; }

    /// <summary>Builds adaptive-quality (HLS) renditions of an already processed MP4. Returns the folder key.</summary>
    Task<string?> GenerateHlsAsync(string fileKey, CancellationToken cancellationToken);
}

public sealed record ProcessedVideo(
    string FileKey,
    string ContentType,
    long SizeBytes,
    double? DurationSeconds,
    string? ThumbnailKey,
    IReadOnlyList<ExtractedSubtitle> Subtitles);

public sealed record ExtractedSubtitle(string FileKey, string Label, string? Language);
