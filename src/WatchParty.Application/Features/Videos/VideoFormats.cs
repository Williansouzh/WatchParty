namespace WatchParty.Application.Features.Videos;

public static class VideoFormats
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".webm"] = "video/webm",
        [".mkv"] = "video/x-matroska",
        [".avi"] = "video/x-msvideo",
        [".ts"] = "video/mp2t",
        [".wmv"] = "video/x-ms-wmv",
        [".flv"] = "video/x-flv"
    };

    public static IReadOnlyCollection<string> SupportedExtensions => ContentTypes.Keys;

    public static bool IsSupported(string fileName) => ContentTypes.ContainsKey(Path.GetExtension(fileName));

    public static string GetContentType(string fileName) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(fileName), "application/octet-stream");
}
