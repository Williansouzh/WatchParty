using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Chat;
using WatchParty.Application.Features.Rooms;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Rooms;

namespace WatchParty.Api.Hubs;

/// <summary>Events the server pushes to browsers in a room.</summary>
public interface IRoomClient
{
    Task ParticipantJoined(RoomParticipant participant);
    Task ParticipantLeft(string connectionId);
    Task ParticipantUpdated(RoomParticipant participant);
    Task PlaybackChanged(PlaybackEvent playbackEvent);
    Task VideoChanged(VideoChangedEvent videoChangedEvent);
    Task ChatMessage(ChatMessageDto message);
    Task VoiceSignal(string fromConnectionId, string type, string payload);
    Task RoomDeleted();
}

public sealed record PlaybackEvent(PlaybackStateDto State, PlaybackAction Action, string ByDisplayName);

public sealed record VideoChangedEvent(VideoDto Video, PlaybackStateDto State, string ByDisplayName);

public sealed record JoinRoomResponse(
    RoomDto Room,
    IReadOnlyList<RoomParticipant> Participants,
    IReadOnlyList<ChatMessageDto> Messages,
    string ConnectionId,
    long ServerTimeMs);
