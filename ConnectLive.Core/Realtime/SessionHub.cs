using System.Text.RegularExpressions;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConnectLive.Core.Api.Realtime;

/// <summary>
/// Hub SignalR des sessions de quiz live. Chaque session correspond à un groupe SignalR ; les clients
/// y entrent via <see cref="JoinSession"/> puis reçoivent les événements définis dans <see cref="SessionHubEvents"/>.
/// Le hub ne gère que la présence : les événements métier (question, classement) sont poussés côté serveur
/// via <see cref="ISessionNotifier"/>.
/// </summary>
[Authorize]
public partial class SessionHub : Hub
{
    private const int MaxIdLength = 64;
    private readonly ISessionPresenceTracker _tracker;

    public SessionHub(ISessionPresenceTracker tracker)
    {
        _tracker = tracker;
    }

    /// <summary>Nom du groupe SignalR associé à une session.</summary>
    public static string GroupName(string sessionId) => $"session:{sessionId}";

    /// <summary>
    /// Rejoint la session et notifie les autres participants. L'identité du participant est celle du jeton
    /// (<c>sub</c>) : le client ne peut pas se faire passer pour un autre utilisateur.
    /// </summary>
    /// <returns>Les participants actuellement connectés (incluant l'appelant).</returns>
    /// <exception cref="HubException">Identifiant de session invalide ou connexion sans identité.</exception>
    public async Task<IReadOnlyCollection<string>> JoinSession(string sessionId)
    {
        EnsureValidId(sessionId, nameof(sessionId));
        var userId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Authentication required.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
        await _tracker.JoinAsync(Context.ConnectionId, sessionId, userId);
        await Clients.Group(GroupName(sessionId)).SendAsync(SessionHubEvents.UserJoined, userId);

        return await _tracker.GetParticipantsAsync(sessionId);
    }

    /// <summary>Quitte la session et notifie les autres participants.</summary>
    /// <exception cref="HubException">Identifiant de session invalide.</exception>
    public async Task LeaveSession(string sessionId)
    {
        EnsureValidId(sessionId, nameof(sessionId));

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));
        await NotifyLeftAsync(await _tracker.LeaveAsync(Context.ConnectionId));
    }

    /// <summary>Une déconnexion brutale (onglet fermé, perte réseau) équivaut à un départ de session.</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await NotifyLeftAsync(await _tracker.LeaveAsync(Context.ConnectionId));
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
