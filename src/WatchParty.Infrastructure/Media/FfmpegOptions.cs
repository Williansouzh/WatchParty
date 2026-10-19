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

    public HlsOptions Hls { get; init; } = new();
}

/// <summary>Adaptive streaming: lighter versions for slow connections. Costs extra encoding time and disk.</summary>
public sealed class HlsOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>Renditions taller than the source are skipped.</summary>
    public List<HlsRendition> Renditions { get; init; } =
    [
        new() { Height = 480, VideoKbps = 1000 },
        new() { Height = 720, VideoKbps = 2500 },
        new() { Height = 1080, VideoKbps = 5000 }
    ];

    public int SegmentSeconds { get; init; } = 6;
}

public sealed class HlsRendition
{
    public int Height { get; init; }
    public int VideoKbps { get; init; }
}
