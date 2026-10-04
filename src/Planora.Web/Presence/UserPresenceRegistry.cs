using System.Collections.Concurrent;

namespace Planora.Web.Presence;

public sealed class UserPresenceRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _connections = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastTouch = new();
    private readonly object _gate = new();

    public bool Connect(string userId, string connectionId)
    {
        lock (_gate)
        {
            var set = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            bool wasOffline = set.IsEmpty;
            set[connectionId] = 0;
            return wasOffline;
        }
    }

    public bool Disconnect(string userId, string connectionId)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(userId, out var set)) return false;
            set.TryRemove(connectionId, out _);
            if (!set.IsEmpty) return false;
            _connections.TryRemove(userId, out _);
            _lastTouch.TryRemove(userId, out _);
            return true;
        }
    }

    public bool IsOnline(string userId)
    {
        lock (_gate) return _connections.TryGetValue(userId, out var set) && !set.IsEmpty;
    }

    public bool TryTouch(string userId, TimeSpan throttle)
    {
        var now = DateTimeOffset.UtcNow;
        while (true)
        {
            if (!_lastTouch.TryGetValue(userId, out var previous))
                return _lastTouch.TryAdd(userId, now);
            if (now - previous < throttle) return false;
            if (_lastTouch.TryUpdate(userId, now, previous)) return true;
        }
    }
}
