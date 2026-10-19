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

    /// <summary>Whether the room was paused automatically to wait for someone who is loading.</summary>
    bool IsAutoPaused(string roomCode);
    void SetAutoPaused(string roomCode, bool autoPaused);

    /// <summary>True at most once per <paramref name="interval"/> for a connection and key (simple rate limit).</summary>
    bool TryThrottle(string connectionId, string key, TimeSpan interval);
}

public sealed record RoomParticipant(
    string ConnectionId,
    Guid UserId,
    string DisplayName,
    string AvatarColor,
    bool InVoice = false,
    bool IsMuted = false,
    bool IsBuffering = false);
