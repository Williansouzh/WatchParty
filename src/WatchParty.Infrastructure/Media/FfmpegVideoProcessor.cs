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
/// nothing, a remux, an audio-only re-encode, or a full re-encode. Also grabs a thumbnail.
/// </summary>
internal sealed class FfmpegVideoProcessor(
    IVideoStorage storage,
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegVideoProcessor> logger) : IVideoProcessor
{
    private const string Mp4ContentType = "video/mp4";

    private enum Conversion { None, Remux, AudioOnly, Full }

    private sealed record Probe(double? DurationSeconds, string FormatName, string? VideoCodec, string? PixelFormat, string? AudioCodec);

    public async Task<ProcessedVideo> ProcessAsync(string sourceKey, CancellationToken cancellationToken)
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
            return new ProcessedVideo(sourceKey, VideoFormats.GetContentType(sourceKey), new FileInfo(sourcePath).Length, null, null);
        }

        if (probe.VideoCodec is null) throw new InvalidOperationException("The file has no video stream.");

        var conversion = ChooseConversion(probe, Path.GetExtension(sourceKey));
        logger.LogInformation("Processing {Key}: {Video}/{Audio} in {Format} -> {Conversion}",
            sourceKey, probe.VideoCodec, probe.AudioCodec ?? "no audio", probe.FormatName, conversion);

        var outputKey = sourceKey;
        if (conversion != Conversion.None)
        {
            outputKey = storage.CreateKey(".mp4");
            await ConvertAsync(sourcePath, storage.GetPhysicalPath(outputKey), conversion, cancellationToken);
        }

        var outputPath = storage.GetPhysicalPath(outputKey);
        var thumbnailKey = await TryCreateThumbnailAsync(outputPath, probe.DurationSeconds, cancellationToken);

        return new ProcessedVideo(outputKey, Mp4ContentType, new FileInfo(outputPath).Length, probe.DurationSeconds, thumbnailKey);
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

    private async Task ConvertAsync(string input, string output, Conversion conversion, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        List<string> args = ["-hide_banner", "-nostdin", "-loglevel", "error", "-y", "-i", input, "-map", "0:v:0", "-map", "0:a:0?"];

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
            var result = await ProcessRunner.RunAsync(settings.FfmpegPath, args, cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg failed ({result.ExitCode}): {result.StandardError}");
        }
        catch
        {
            if (File.Exists(output)) File.Delete(output);
            throw;
        }
    }

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
            }
            else if (type == "audio" && audioCodec is null)
            {
                audioCodec = codec;
            }
        }

        var format = root.GetProperty("format");
        double? duration = format.TryGetProperty("duration", out var dur)
            && double.TryParse(dur.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : null;
        var formatName = format.TryGetProperty("format_name", out var name) ? name.GetString() ?? "" : "";

        return new Probe(duration, formatName, videoCodec, pixelFormat, audioCodec);
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
