namespace ConnectLive.Core.Api.Realtime;

/// <summary>Association d'une connexion SignalR à un participant d'une session.</summary>
public sealed record SessionPresence(string SessionId, string UserId);

/// <summary>
/// Suit quelle connexion SignalR appartient à quel participant de quelle session.
/// Deux implémentations interchangeables : <see cref="SessionPresenceTracker"/> (mémoire d'un process, développement
/// et instance unique) et <see cref="RedisSessionPresenceTracker"/> (partagée entre instances, voir docs/SIGNALR-BACKPLANE-REDIS.md).
/// </summary>
public interface ISessionPresenceTracker
{
    /// <summary>Enregistre la connexion ; si elle était déjà dans une autre session, elle y est remplacée.</summary>
    Task JoinAsync(string connectionId, string sessionId, string userId);

    /// <summary>Retire la connexion et renvoie sa présence, ou <c>null</c> si elle n'était pas suivie.</summary>
    Task<SessionPresence?> LeaveAsync(string connectionId);

    /// <summary>Instantané des utilisateurs distincts connectés à la session (un utilisateur peut avoir plusieurs onglets).</summary>
    Task<IReadOnlyCollection<string>> GetParticipantsAsync(string sessionId);
}
