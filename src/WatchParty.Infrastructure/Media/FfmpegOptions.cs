namespace WatchParty.Infrastructure.Media;

public sealed class FfmpegOptions
{
    public const string SectionName = "Ffmpeg";

    public string FfmpegPath { get; init; } = "ffmpeg";
    public string FfprobePath { get; init; } = "ffprobe";

    /// <summary>x264 preset used when a full re-encode is needed. Slower = smaller files.</summary>
    public string Preset { get; init; } = "veryfast";
    public int Crf { get; init; } = 23;
    public int MaxHeight { get; init; } = 1080;
}
