namespace WatchParty.Application.Abstractions.Services;

/// <summary>Converts an uploaded file into something every browser can play and extracts metadata.</summary>
public interface IVideoProcessor
{
    Task<ProcessedVideo> ProcessAsync(string sourceKey, CancellationToken cancellationToken);
}

public sealed record ProcessedVideo(
    string FileKey,
    string ContentType,
    long SizeBytes,
    double? DurationSeconds,
    string? ThumbnailKey);
