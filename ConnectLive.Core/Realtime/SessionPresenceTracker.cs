using System.Collections.Concurrent;

namespace ConnectLive.Core.Api.Realtime;

/// <summary>Association d'une connexion SignalR à un participant d'une session.</summary>
public sealed record SessionPresence(string SessionId, string UserId);

/// <summary>
/// Suit quelle connexion SignalR appartient à quel participant de quelle session.
/// Singleton thread-safe, en mémoire de process : en déploiement multi-instances, la présence est
/// locale à chaque instance (à migrer vers Redis avec un backplane SignalR — voir docs/SIGNALR-REALTIME.md).
/// </summary>
public sealed class SessionPresenceTracker
{
    private readonly ConcurrentDictionary<string, SessionPresence> _connections = new();

    /// <summary>Enregistre la connexion ; si elle était déjà dans une autre session, elle y est remplacée.</summary>
    public void Join(string connectionId, string sessionId, string userId) =>
        _connections[connectionId] = new SessionPresence(sessionId, userId);

    /// <summary>Retire la connexion et renvoie sa présence, ou <c>null</c> si elle n'était pas suivie.</summary>
    public SessionPresence? Leave(string connectionId) =>
        _connections.TryRemove(connectionId, out var presence) ? presence : null;

    /// <summary>Instantané des utilisateurs distincts connectés à la session (un utilisateur peut avoir plusieurs onglets).</summary>
    public IReadOnlyCollection<string> GetParticipants(string sessionId) =>
        _connections.Values
            .Where(p => p.SessionId == sessionId)
            .Select(p => p.UserId)
            .Distinct()
            .ToArray();
}
