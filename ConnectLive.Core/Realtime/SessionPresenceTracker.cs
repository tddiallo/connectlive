using System.Collections.Concurrent;

namespace ConnectLive.Core.Api.Realtime;

/// <summary>
/// Implémentation en mémoire de process (thread-safe). Suffisante pour le développement et une instance unique ;
/// avec plusieurs instances la présence serait locale à chacune : utiliser <see cref="RedisSessionPresenceTracker"/>.
/// </summary>
public sealed class SessionPresenceTracker : ISessionPresenceTracker
{
    private readonly ConcurrentDictionary<string, SessionPresence> _connections = new();

    public Task JoinAsync(string connectionId, string sessionId, string userId)
    {
        _connections[connectionId] = new SessionPresence(sessionId, userId);
        return Task.CompletedTask;
    }

    public Task<SessionPresence?> LeaveAsync(string connectionId) =>
        Task.FromResult(_connections.TryRemove(connectionId, out var presence) ? presence : null);

    public Task<IReadOnlyCollection<string>> GetParticipantsAsync(string sessionId) =>
        Task.FromResult<IReadOnlyCollection<string>>(
            _connections.Values
                .Where(p => p.SessionId == sessionId)
                .Select(p => p.UserId)
                .Distinct()
                .ToArray());
}
