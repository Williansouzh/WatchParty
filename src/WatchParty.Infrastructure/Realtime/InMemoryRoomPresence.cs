using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Infrastructure.Realtime;

/// <summary>Who is in which room, keyed by SignalR connection. Fine for a single server.</summary>
internal sealed class InMemoryRoomPresence : IRoomPresence
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (string RoomCode, RoomParticipant Participant)> _connections = [];

    public void Join(string roomCode, RoomParticipant participant)
    {
        lock (_lock) _connections[participant.ConnectionId] = (roomCode, participant);
    }

    public (string RoomCode, RoomParticipant Participant)? Leave(string connectionId)
    {
        lock (_lock)
        {
            return _connections.Remove(connectionId, out var entry) ? entry : null;
        }
    }

    public string? GetRoomOf(string connectionId)
    {
        lock (_lock) return _connections.TryGetValue(connectionId, out var entry) ? entry.RoomCode : null;
    }

    public RoomParticipant? Get(string connectionId)
    {
        lock (_lock) return _connections.TryGetValue(connectionId, out var entry) ? entry.Participant : null;
    }

    public RoomParticipant? Update(string connectionId, Func<RoomParticipant, RoomParticipant> update)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionId, out var entry)) return null;

            var updated = update(entry.Participant);
            _connections[connectionId] = (entry.RoomCode, updated);
            return updated;
        }
    }

    public IReadOnlyList<RoomParticipant> GetParticipants(string roomCode)
    {
        lock (_lock)
        {
            return _connections.Values.Where(e => e.RoomCode == roomCode).Select(e => e.Participant).ToList();
        }
    }

    public int CountUsers(string roomCode)
    {
        lock (_lock)
        {
            return _connections.Values.Where(e => e.RoomCode == roomCode).Select(e => e.Participant.UserId).Distinct().Count();
        }
    }
}
