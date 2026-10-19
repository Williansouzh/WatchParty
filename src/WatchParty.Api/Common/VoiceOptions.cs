using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WatchParty.Api.Common;

/// <summary>
/// Voice chat transport. "mesh" (default) connects everyone directly over WebRTC, which is fine for a
/// handful of friends. "livekit" routes audio through a LiveKit server (self-hosted or LiveKit Cloud),
/// which scales to bigger calls and works behind restrictive networks.
/// </summary>
public sealed class VoiceOptions
{
    public const string SectionName = "Voice";

    public string Provider { get; init; } = "mesh";
    public LiveKitOptions LiveKit { get; init; } = new();

    public bool UseLiveKit =>
        string.Equals(Provider, "livekit", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(LiveKit.Url)
        && !string.IsNullOrWhiteSpace(LiveKit.ApiKey)
        && !string.IsNullOrWhiteSpace(LiveKit.ApiSecret);

    public sealed class LiveKitOptions
    {
        /// <summary>WebSocket URL clients connect to, e.g. wss://meu-projeto.livekit.cloud.</summary>
        public string Url { get; init; } = "";
        public string ApiKey { get; init; } = "";
        public string ApiSecret { get; init; } = "";
        public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(6);
    }
}

/// <summary>Issues LiveKit access tokens (HS256 JWTs with a "video" grant) without pulling in an SDK.</summary>
public static class LiveKitToken
{
    public static string Create(VoiceOptions.LiveKitOptions options, string room, string identity, string name, DateTimeOffset now)
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = options.ApiKey,
            ["sub"] = identity,
            ["name"] = name,
            ["nbf"] = now.ToUnixTimeSeconds() - 10,
            ["exp"] = now.Add(options.TokenLifetime).ToUnixTimeSeconds(),
            ["video"] = new Dictionary<string, object>
            {
                ["room"] = room,
                ["roomJoin"] = true,
                ["canPublish"] = true,
                ["canSubscribe"] = true,
                ["canPublishData"] = false,
                ["canPublishSources"] = new[] { "microphone" },
            },
        };

        var unsigned = $"{Encode(JsonSerializer.SerializeToUtf8Bytes(header))}.{Encode(JsonSerializer.SerializeToUtf8Bytes(payload))}";
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.ApiSecret), Encoding.UTF8.GetBytes(unsigned));
        return $"{unsigned}.{Encode(signature)}";
    }

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
