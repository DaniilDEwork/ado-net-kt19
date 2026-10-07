namespace Kt19Chat.Services;

public class ChatSessionStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, ChatSession> sessions = [];

    public string Create(DateTimeOffset expiresAt)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (sync) sessions.Add(id, new ChatSession(expiresAt));
        return id;
    }

    public bool IsActive(string? id)
    {
        lock (sync)
            return id is not null && sessions.TryGetValue(id, out var session) && session.ExpiresAt > DateTimeOffset.UtcNow;
    }

    public bool AddConnection(string? sessionId, string connectionId, Action abort)
    {
        lock (sync)
        {
            if (sessionId is null || !sessions.TryGetValue(sessionId, out var session)
                || session.ExpiresAt <= DateTimeOffset.UtcNow) return false;
            session.Connections[connectionId] = abort;
            return true;
        }
    }

    public void RemoveConnection(string? sessionId, string connectionId)
    {
        lock (sync)
        {
            if (sessionId is not null && sessions.TryGetValue(sessionId, out var session))
                session.Connections.Remove(connectionId);
        }
    }

    public void Remove(string? id)
    {
        Action[] connections;
        lock (sync)
        {
            if (id is null || !sessions.Remove(id, out var session)) return;
            connections = session.Connections.Values.ToArray();
        }
        foreach (var abort in connections) abort();
    }

    private class ChatSession(DateTimeOffset expiresAt)
    {
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public Dictionary<string, Action> Connections { get; } = [];
    }
}
