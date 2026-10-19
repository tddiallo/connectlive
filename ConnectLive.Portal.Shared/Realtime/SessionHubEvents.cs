namespace ConnectLive.Portal.Shared.Realtime;

/// <summary>
/// Contrat temps réel du hub de session : chemin du hub et noms des événements poussés aux clients.
/// Partagé entre l'API et les clients (SPA Blazor) pour éviter les chaînes magiques dupliquées.
/// </summary>
public static class SessionHubEvents
{
    /// <summary>Chemin de l'endpoint SignalR exposé par l'API.</summary>
    public const string HubPath = "/sessionHub";

    /// <summary>Un participant a rejoint la session. Argument : <c>string userId</c>.</summary>
    public const string UserJoined = "UserJoined";

    /// <summary>Un participant a quitté la session (départ explicite ou déconnexion). Argument : <c>string userId</c>.</summary>
    public const string UserLeft = "UserLeft";

    /// <summary>La question courante a changé. Argument : <c>SessionQuestionDto</c>.</summary>
    public const string QuestionChanged = "QuestionChanged";

    /// <summary>Le classement a été mis à jour. Argument : <c>IReadOnlyList&lt;LeaderboardEntryDto&gt;</c>.</summary>
    public const string LeaderboardUpdated = "LeaderboardUpdated";

    /// <summary>La session est terminée. Argument : <c>string sessionId</c>.</summary>
    public const string SessionEnded = "SessionEnded";
}
