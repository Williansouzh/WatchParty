using System.Text;
using System.Text.RegularExpressions;

namespace WatchParty.Application.Features.Videos;

/// <summary>Turns .srt/.vtt bytes into WebVTT, the format browsers and the app understand.</summary>
public static partial class SubtitleConverter
{
    public static readonly string[] SupportedExtensions = [".srt", ".vtt"];

    /// <summary>Returns null when the content doesn't look like subtitles.</summary>
    public static string? ToWebVtt(byte[] content)
    {
        var text = Decode(content).Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('﻿').Trim();
        if (!text.Contains("-->")) return null;

        if (text.StartsWith("WEBVTT", StringComparison.Ordinal)) return text + "\n";

        // SRT uses a comma before the milliseconds; WebVTT uses a dot.
        var body = SrtTimestamp().Replace(text, "$1.$2");
        return "WEBVTT\n\n" + body + "\n";
    }

    /// <summary>UTF-8 when valid, otherwise Windows-1252/Latin-1 (very common in Brazilian .srt files).</summary>
    private static string Decode(byte[] content)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(content);
        }
    }

    [GeneratedRegex(@"(\d{1,2}:\d{2}:\d{2}),(\d{3})")]
    private static partial Regex SrtTimestamp();
}
