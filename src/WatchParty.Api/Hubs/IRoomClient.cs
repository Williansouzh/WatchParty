using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Chat;
using WatchParty.Application.Features.Rooms;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Rooms;

namespace WatchParty.Api.Hubs;

/// <summary>Events the server pushes to browsers and apps.</summary>
public interface IRoomClient
{
    Task ParticipantJoined(RoomParticipant participant);
    Task ParticipantLeft(string connectionId);
    Task ParticipantUpdated(RoomParticipant participant);
    Task PlaybackChanged(PlaybackEvent playbackEvent);
    Task VideoChanged(VideoChangedEvent videoChangedEvent);
    Task VideoRemoved(VideoRemovedEvent videoRemovedEvent);
    Task QueueChanged(IReadOnlyList<QueueItemDto> queue);
    Task RoomSettingsChanged(RoomSettingsEvent settings);
    Task BufferingChanged(IReadOnlyList<string> waitingFor);
    Task Reaction(ReactionEvent reaction);
    Task ChatMessage(ChatMessageDto message);
    Task VoiceSignal(string fromConnectionId, string type, string payload);
    Task RoomDeleted();

    /// <summary>Sent to everyone watching the lobby (outside rooms): a room was created or came alive.</summary>
    Task RoomActivity(RoomActivityEvent activity);
}

/// <param name="Reason">null for a person's action, "buffering" when paused to wait for someone, "resumed" when everyone is ready.</param>
public sealed record PlaybackEvent(PlaybackStateDto State, PlaybackAction Action, string ByDisplayName, string? Reason = null);

/// <param name="Reason">"manual" or "next" (queue advanced).</param>
public sealed record VideoChangedEvent(VideoDto Video, PlaybackStateDto State, string ByDisplayName, IReadOnlyList<QueueItemDto> Queue, string Reason = "manual");

public sealed record VideoRemovedEvent(Guid VideoId, PlaybackStateDto State);

public sealed record RoomSettingsEvent(bool HostOnlyControl, string ByDisplayName);

public sealed record ReactionEvent(string Emoji, string DisplayName, string ConnectionId);

/// <param name="Kind">"created" or "started" (first person came in).</param>
public sealed record RoomActivityEvent(string Kind, string Code, string RoomName, string? VideoTitle, Guid ByUserId, string ByDisplayName);

public sealed record JoinRoomResponse(
    RoomDto Room,
    IReadOnlyList<RoomParticipant> Participants,
    IReadOnlyList<ChatMessageDto> Messages,
    string ConnectionId,
    long ServerTimeMs,
    IReadOnlyList<string> WaitingFor);
