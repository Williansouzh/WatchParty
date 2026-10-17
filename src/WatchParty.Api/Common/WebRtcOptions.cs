namespace WatchParty.Api.Common;

/// <summary>ICE servers handed to the browser for voice chat. Add a TURN server if friends can't hear each other.</summary>
public sealed class WebRtcOptions
{
    public const string SectionName = "WebRtc";

    public List<IceServer> IceServers { get; init; } = [];

    public sealed class IceServer
    {
        public string[] Urls { get; init; } = [];
        public string? Username { get; init; }
        public string? Credential { get; init; }
    }
}
