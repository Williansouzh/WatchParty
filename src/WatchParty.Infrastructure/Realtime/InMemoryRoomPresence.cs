using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Infrastructure.Realtime;

/// <summary>Who is in which room, keyed by SignalR connection. Fine for a single server.</summary>
internal sealed class InMemoryRoomPresence(TimeProvider time) : IRoomPresence
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (string RoomCode, RoomParticipant Participant)> _connections = [];
    private readonly HashSet<string> _autoPaused = [];
    private readonly Dictionary<(string ConnectionId, string Key), DateTimeOffset> _throttle = [];

    public void Join(string roomCode, RoomParticipant participant)
    {
        lock (_lock) _connections[participant.ConnectionId] = (roomCode, participant);
    }

    public (string RoomCode, RoomParticipant Participant)? Leave(string connectionId)
    {
        lock (_lock)
        {
            foreach (var key in _throttle.Keys.Where(k => k.ConnectionId == connectionId).ToList()) _throttle.Remove(key);
            if (!_connections.Remove(connectionId, out var entry)) return null;
            if (_connections.Values.All(e => e.RoomCode != entry.RoomCode)) _autoPaused.Remove(entry.RoomCode);
            return entry;
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

    public bool IsAutoPaused(string roomCode)
    {
        lock (_lock) return _autoPaused.Contains(roomCode);
    }

    public void SetAutoPaused(string roomCode, bool autoPaused)
    {
        lock (_lock)
        {
            if (autoPaused) _autoPaused.Add(roomCode);
            else _autoPaused.Remove(roomCode);
        }
    }

    public bool TryThrottle(string connectionId, string key, TimeSpan interval)
    {
        var now = time.GetUtcNow();
        lock (_lock)
        {
            if (_throttle.TryGetValue((connectionId, key), out var last) && now - last < interval) return false;
            _throttle[(connectionId, key)] = now;
            return true;
        }
    }
}
