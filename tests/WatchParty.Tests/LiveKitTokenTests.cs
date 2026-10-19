using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WatchParty.Api.Common;

namespace WatchParty.Tests;

public sealed class LiveKitTokenTests
{
    [Fact]
    public void Token_is_a_signed_jwt_with_a_room_grant()
    {
        var options = new VoiceOptions.LiveKitOptions { Url = "ws://lk", ApiKey = "devkey", ApiSecret = "secret" };
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        var token = LiveKitToken.Create(options, "watchparty-ABC123", "conn-1", "Willian", now);

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes("secret"), Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));
        Assert.Equal(expected, Decode(parts[2]));

        using var payload = JsonDocument.Parse(Decode(parts[1]));
        var root = payload.RootElement;
        Assert.Equal("devkey", root.GetProperty("iss").GetString());
        Assert.Equal("conn-1", root.GetProperty("sub").GetString());
        Assert.Equal("Willian", root.GetProperty("name").GetString());
        Assert.Equal(now.AddHours(6).ToUnixTimeSeconds(), root.GetProperty("exp").GetInt64());
        var video = root.GetProperty("video");
        Assert.Equal("watchparty-ABC123", video.GetProperty("room").GetString());
        Assert.True(video.GetProperty("roomJoin").GetBoolean());
        Assert.True(video.GetProperty("canPublish").GetBoolean());
    }

    [Theory]
    [InlineData("mesh", true, false)]
    [InlineData("livekit", true, true)]
    [InlineData("LiveKit", true, true)]
    [InlineData("livekit", false, false)]
    public void LiveKit_is_used_only_when_fully_configured(string provider, bool configured, bool expected)
    {
        var options = new VoiceOptions
        {
            Provider = provider,
            LiveKit = configured ? new() { Url = "wss://x", ApiKey = "k", ApiSecret = "s" } : new(),
        };
        Assert.Equal(expected, options.UseLiveKit);
    }

    private static byte[] Decode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
