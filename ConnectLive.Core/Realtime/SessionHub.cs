using System.Text.RegularExpressions;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace ConnectLive.Core.Api.Realtime;

/// <summary>
/// Hub SignalR des sessions de quiz live. Chaque session correspond à un groupe SignalR ; les clients
/// y entrent via <see cref="JoinSession"/> puis reçoivent les événements définis dans <see cref="SessionHubEvents"/>.
/// Le hub ne gère que la présence : les événements métier (question, classement) sont poussés côté serveur
/// via <see cref="ISessionNotifier"/>.
/// </summary>
public partial class SessionHub : Hub
{
    private const int MaxIdLength = 64;
    private readonly SessionPresenceTracker _tracker;

    public SessionHub(SessionPresenceTracker tracker)
    {
        _tracker = tracker;
    }

    /// <summary>Nom du groupe SignalR associé à une session.</summary>
    public static string GroupName(string sessionId) => $"session:{sessionId}";

    /// <summary>
    /// Rejoint la session et notifie les autres participants.
    /// </summary>
    /// <returns>Les participants actuellement connectés (incluant l'appelant).</returns>
    /// <exception cref="HubException">Identifiant de session ou d'utilisateur invalide.</exception>
    public async Task<IReadOnlyCollection<string>> JoinSession(string sessionId, string userId)
    {
        EnsureValidId(sessionId, nameof(sessionId));
        EnsureValidId(userId, nameof(userId));

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
        _tracker.Join(Context.ConnectionId, sessionId, userId);
        await Clients.Group(GroupName(sessionId)).SendAsync(SessionHubEvents.UserJoined, userId);

        return _tracker.GetParticipants(sessionId);
    }

    /// <summary>Quitte la session et notifie les autres participants.</summary>
    /// <exception cref="HubException">Identifiant de session invalide.</exception>
    public async Task LeaveSession(string sessionId)
    {
        EnsureValidId(sessionId, nameof(sessionId));

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));
        await NotifyLeftAsync(_tracker.Leave(Context.ConnectionId));
    }

    /// <summary>Une déconnexion brutale (onglet fermé, perte réseau) équivaut à un départ de session.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await NotifyLeftAsync(_tracker.Leave(Context.ConnectionId));
        await base.OnDisconnectedAsync(exception);
    }

    private Task NotifyLeftAsync(SessionPresence? presence) =>
        presence is null
            ? Task.CompletedTask
            : Clients.Group(GroupName(presence.SessionId)).SendAsync(SessionHubEvents.UserLeft, presence.UserId);

    /// <summary>Les identifiants entrent dans des noms de groupe : on n'accepte que des caractères sûrs.</summary>
    private static void EnsureValidId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdLength || !SafeId().IsMatch(value))
            throw new HubException($"Invalid {parameterName}.");
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex SafeId();
}
