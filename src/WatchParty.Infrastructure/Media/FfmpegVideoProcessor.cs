using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Videos;

namespace WatchParty.Infrastructure.Media;

/// <summary>
/// Makes uploads playable in any browser (MP4 + H.264 + AAC) doing as little work as possible:
/// nothing, a remux, an audio-only re-encode, or a full re-encode. Also grabs a thumbnail, extracts
/// text subtitles as WebVTT and, optionally, builds HLS renditions for slow connections.
/// </summary>
internal sealed class FfmpegVideoProcessor(
    IVideoStorage storage,
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegVideoProcessor> logger) : IVideoProcessor
{
    private const string Mp4ContentType = "video/mp4";
    private static readonly HashSet<string> TextSubtitleCodecs = ["subrip", "ass", "ssa", "mov_text", "webvtt", "text"];

    private enum Conversion { None, Remux, AudioOnly, Full }

    private sealed record SubtitleStream(int Index, string? Language, string? Title);

    private sealed record Probe(
        double? DurationSeconds,
        string FormatName,
        string? VideoCodec,
        string? PixelFormat,
        int? Height,
        string? AudioCodec,
        IReadOnlyList<SubtitleStream> Subtitles);

    public bool HlsEnabled => options.Value.Hls.Enabled;

    public async Task<ProcessedVideo> ProcessAsync(string sourceKey, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var sourcePath = storage.GetPhysicalPath(sourceKey);

        Probe probe;
        try
        {
            probe = await ProbeAsync(sourcePath, cancellationToken);
        }
        catch (Win32Exception)
        {
            logger.LogWarning("ffprobe was not found. The video is published as uploaded and may not play on every device.");
            return new ProcessedVideo(sourceKey, VideoFormats.GetContentType(sourceKey), new FileInfo(sourcePath).Length, null, null, []);
        }

        if (probe.VideoCodec is null) throw new InvalidOperationException("The file has no video stream.");

        // Subtitles come from the original file, which may be deleted after conversion.
        var subtitles = await ExtractSubtitlesAsync(sourcePath, probe.Subtitles, cancellationToken);

        var conversion = ChooseConversion(probe, Path.GetExtension(sourceKey));
        logger.LogInformation("Processing {Key}: {Video}/{Audio} in {Format} -> {Conversion}, {Subtitles} subtitle(s)",
            sourceKey, probe.VideoCodec, probe.AudioCodec ?? "no audio", probe.FormatName, conversion, subtitles.Count);

        var outputKey = sourceKey;
        if (conversion != Conversion.None)
        {
            outputKey = storage.CreateKey(".mp4");
            await ConvertAsync(sourcePath, storage.GetPhysicalPath(outputKey), conversion, probe.DurationSeconds, progress, cancellationToken);
        }
        progress?.Report(1);

        var outputPath = storage.GetPhysicalPath(outputKey);
        var thumbnailKey = await TryCreateThumbnailAsync(outputPath, probe.DurationSeconds, cancellationToken);

        return new ProcessedVideo(outputKey, Mp4ContentType, new FileInfo(outputPath).Length, probe.DurationSeconds, thumbnailKey, subtitles);
    }

    public async Task<string?> GenerateHlsAsync(string fileKey, CancellationToken cancellationToken)
    {
        var settings = options.Value.Hls;
        if (!settings.Enabled) return null;

        var input = storage.GetPhysicalPath(fileKey);
        Probe probe;
        try
        {
            probe = await ProbeAsync(input, cancellationToken);
        }
        catch (Win32Exception)
        {
            return null;
        }

        var sourceHeight = probe.Height ?? 1080;
        var renditions = settings.Renditions.Where(r => r.Height <= sourceHeight).OrderBy(r => r.Height).ToList();
        if (renditions.Count == 0) renditions = [new HlsRendition { Height = sourceHeight - sourceHeight % 2, VideoKbps = 800 }];

        var folderKey = storage.CreateFolderKey("hls");
        var folder = storage.GetFolderPath(folderKey);
        for (var i = 0; i < renditions.Count; i++) Directory.CreateDirectory(Path.Combine(folder, $"v{i}"));

        var split = string.Concat(Enumerable.Range(0, renditions.Count).Select(i => $"[s{i}]"));
        var filter = $"[0:v]split={renditions.Count}{split};" +
                     string.Join(';', renditions.Select((r, i) => $"[s{i}]scale=-2:{r.Height}[o{i}]"));

        List<string> args = ["-hide_banner", "-nostdin", "-loglevel", "error", "-y", "-i", input, "-filter_complex", filter];
        for (var i = 0; i < renditions.Count; i++)
        {
            var kbps = renditions[i].VideoKbps;
            args.AddRange(["-map", $"[o{i}]", $"-c:v:{i}", "libx264", $"-b:v:{i}", $"{kbps}k",
                $"-maxrate:v:{i}", $"{kbps * 6 / 5}k", $"-bufsize:v:{i}", $"{kbps * 2}k"]);
        }
        var hasAudio = probe.AudioCodec is not null;
        if (hasAudio)
        {
            for (var i = 0; i < renditions.Count; i++) args.AddRange(["-map", "0:a:0"]);
            args.AddRange(["-c:a", "aac", "-b:a", "128k", "-ac", "2"]);
        }

        var segment = settings.SegmentSeconds.ToString(CultureInfo.InvariantCulture);
        args.AddRange([
            "-preset", options.Value.Preset, "-pix_fmt", "yuv420p",
            "-force_key_frames", $"expr:gte(t,n_forced*{segment})", "-sc_threshold", "0",
            "-f", "hls", "-hls_time", segment, "-hls_playlist_type", "vod", "-hls_flags", "independent_segments",
            "-hls_segment_filename", Path.Combine(folder, "v%v", "seg_%04d.ts"),
            "-master_pl_name", "master.m3u8",
            "-var_stream_map", string.Join(' ', Enumerable.Range(0, renditions.Count).Select(i => hasAudio ? $"v:{i},a:{i}" : $"v:{i}")),
            Path.Combine(folder, "v%v", "index.m3u8")
        ]);

        try
        {
            var result = await ProcessRunner.RunAsync(options.Value.FfmpegPath, args, cancellationToken);
            if (result.ExitCode != 0 || !File.Exists(Path.Combine(folder, "master.m3u8")))
                throw new InvalidOperationException($"ffmpeg HLS failed ({result.ExitCode}): {result.StandardError}");
        }
        catch
        {
            storage.DeleteFolder(folderKey);
            throw;
        }

        logger.LogInformation("HLS ready for {Key}: {Heights}", fileKey, string.Join("/", renditions.Select(r => r.Height + "p")));
        return folderKey;
    }

    private static Conversion ChooseConversion(Probe probe, string extension)
    {
        var videoOk = probe.VideoCodec == "h264" && probe.PixelFormat is "yuv420p" or "yuvj420p";
        var audioOk = probe.AudioCodec is null or "aac" or "mp3";
        var isMp4 = probe.FormatName.Contains("mp4") && extension is ".mp4" or ".m4v";

        return (videoOk, audioOk) switch
        {
            (true, true) => isMp4 ? Conversion.None : Conversion.Remux,
            (true, false) => Conversion.AudioOnly,
            _ => Conversion.Full
        };
    }

    private async Task ConvertAsync(
        string input,
        string output,
        Conversion conversion,
        double? duration,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        List<string> args = ["-hide_banner", "-nostdin", "-loglevel", "error", "-nostats", "-progress", "pipe:1", "-y",
            "-i", input, "-map", "0:v:0", "-map", "0:a:0?"];

        if (conversion == Conversion.Full)
        {
            args.AddRange([
                "-c:v", "libx264", "-preset", settings.Preset, "-crf", settings.Crf.ToString(CultureInfo.InvariantCulture),
                "-pix_fmt", "yuv420p", "-vf", $"scale=-2:'min({settings.MaxHeight},ih)'"
            ]);
        }
        else
        {
            args.AddRange(["-c:v", "copy"]);
        }

        args.AddRange(conversion == Conversion.AudioOnly || conversion == Conversion.Full
            ? ["-c:a", "aac", "-b:a", "192k", "-ac", "2"]
            : ["-c:a", "copy"]);

        args.AddRange(["-sn", "-dn", "-movflags", "+faststart", output]);

        try
        {
            var result = await ProcessRunner.RunAsync(settings.FfmpegPath, args, cancellationToken, line =>
            {
                // "-progress" prints key=value lines; out_time_us is how far the encode is.
                if (duration is > 0 && line.StartsWith("out_time_us=", StringComparison.Ordinal)
                    && long.TryParse(line.AsSpan(12), out var micros))
                {
                    progress?.Report(Math.Clamp(micros / 1_000_000d / duration.Value, 0, 0.99));
                }
            });
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg failed ({result.ExitCode}): {result.StandardError}");
        }
        catch
        {
            if (File.Exists(output)) File.Delete(output);
            throw;
        }
    }

    private async Task<IReadOnlyList<ExtractedSubtitle>> ExtractSubtitlesAsync(
        string input,
        IReadOnlyList<SubtitleStream> streams,
        CancellationToken cancellationToken)
    {
        var extracted = new List<ExtractedSubtitle>();
        foreach (var stream in streams)
        {
            var key = storage.CreateKey(".vtt");
            var path = storage.GetPhysicalPath(key);
            try
            {
                var result = await ProcessRunner.RunAsync(options.Value.FfmpegPath,
                    ["-hide_banner", "-nostdin", "-loglevel", "error", "-y", "-i", input, "-map", $"0:s:{stream.Index}", "-c:s", "webvtt", path],
                    cancellationToken);
                if (result.ExitCode == 0 && File.Exists(path) && new FileInfo(path).Length > 10)
                {
                    var label = stream.Title ?? LanguageName(stream.Language) ?? $"Legenda {extracted.Count + 1}";
                    extracted.Add(new ExtractedSubtitle(key, label, stream.Language));
                    continue;
                }
                logger.LogWarning("Could not extract subtitle stream {Index}: {Error}", stream.Index, result.StandardError);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not extract subtitle stream {Index}", stream.Index);
            }
            if (File.Exists(path)) File.Delete(path);
        }
        return extracted;
    }

    private static string? LanguageName(string? code) => code?.ToLowerInvariant() switch
    {
        null or "und" => null,
        "por" or "pt" or "pob" or "pt-br" => "Português",
        "eng" or "en" => "Inglês",
        "spa" or "es" => "Espanhol",
        "fre" or "fra" or "fr" => "Francês",
        "ger" or "deu" or "de" => "Alemão",
        "ita" or "it" => "Italiano",
        "jpn" or "ja" => "Japonês",
        "kor" or "ko" => "Coreano",
        _ => code.ToUpperInvariant()
    };

    private async Task<Probe> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(
            options.Value.FfprobePath,
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", path],
            cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed ({result.ExitCode}): {result.StandardError}");

        using var json = JsonDocument.Parse(result.StandardOutput);
        var root = json.RootElement;

        string? videoCodec = null, pixelFormat = null, audioCodec = null;
        int? height = null;
        var subtitles = new List<SubtitleStream>();
        var subtitleIndex = 0;
        foreach (var stream in root.GetProperty("streams").EnumerateArray())
        {
            var type = stream.GetProperty("codec_type").GetString();
            var codec = stream.TryGetProperty("codec_name", out var c) ? c.GetString() : null;

            // Skip cover art embedded as a "video" stream.
            var isAttachedPicture = stream.TryGetProperty("disposition", out var d)
                && d.TryGetProperty("attached_pic", out var pic) && pic.GetInt32() == 1;

            if (type == "video" && videoCodec is null && !isAttachedPicture)
            {
                videoCodec = codec;
                pixelFormat = stream.TryGetProperty("pix_fmt", out var p) ? p.GetString() : null;
                height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : null;
            }
            else if (type == "audio" && audioCodec is null)
            {
                audioCodec = codec;
            }
            else if (type == "subtitle")
            {
                if (codec is not null && TextSubtitleCodecs.Contains(codec))
                {
                    stream.TryGetProperty("tags", out var tags);
                    subtitles.Add(new SubtitleStream(
                        subtitleIndex,
                        tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty("language", out var lang) ? lang.GetString() : null,
                        tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty("title", out var title) ? title.GetString() : null));
                }
                subtitleIndex++; // image-based subtitles (PGS, DVD) can't become text, but still count for 0:s:N
            }
        }

        var format = root.GetProperty("format");
        double? duration = format.TryGetProperty("duration", out var dur)
            && double.TryParse(dur.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : null;
        var formatName = format.TryGetProperty("format_name", out var name) ? name.GetString() ?? "" : "";

        return new Probe(duration, formatName, videoCodec, pixelFormat, height, audioCodec, subtitles);
    }

    private async Task<string?> TryCreateThumbnailAsync(string videoPath, double? duration, CancellationToken cancellationToken)
    {
        var key = storage.CreateKey(".jpg");
        var path = storage.GetPhysicalPath(key);
        var at = duration is > 0 ? Math.Min(duration.Value * 0.15, 120) : 1;

        try
        {
            var result = await ProcessRunner.RunAsync(options.Value.FfmpegPath,
            [
                "-hide_banner", "-nostdin", "-loglevel", "error", "-y",
                "-ss", at.ToString("0.###", CultureInfo.InvariantCulture), "-i", videoPath,
                "-frames:v", "1", "-vf", "scale=640:-2", "-q:v", "3", path
            ], cancellationToken);

            if (result.ExitCode == 0 && File.Exists(path)) return key;
            logger.LogWarning("Could not create thumbnail: {Error}", result.StandardError);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not create thumbnail");
        }

        if (File.Exists(path)) File.Delete(path);
        return null;
    }
}
