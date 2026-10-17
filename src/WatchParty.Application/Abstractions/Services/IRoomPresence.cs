namespace WatchParty.Application.Abstractions.Services;

/// <summary>Tracks who is connected to each room right now (in memory, not persisted).</summary>
public interface IRoomPresence
{
    void Join(string roomCode, RoomParticipant participant);
    (string RoomCode, RoomParticipant Participant)? Leave(string connectionId);
    string? GetRoomOf(string connectionId);
    RoomParticipant? Get(string connectionId);
    RoomParticipant? Update(string connectionId, Func<RoomParticipant, RoomParticipant> update);
    IReadOnlyList<RoomParticipant> GetParticipants(string roomCode);
    int CountUsers(string roomCode);
}

public sealed record RoomParticipant(
    string ConnectionId,
    Guid UserId,
    string DisplayName,
    string AvatarColor,
    bool InVoice = false,
    bool IsMuted = false);
