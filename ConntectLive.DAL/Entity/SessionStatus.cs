namespace ConntectLive.DAL;

/// <summary>Cycle de vie d'une session de quiz : <c>Lobby → Running → Ended</c>.</summary>
public enum SessionStatus
{
    /// <summary>Créée, les joueurs peuvent la rejoindre, aucune question n'est encore posée.</summary>
    Lobby = 0,

    /// <summary>En cours : une question courante est posée.</summary>
    Running = 1,

    /// <summary>Terminée (dernière question passée ou arrêt anticipé).</summary>
    Ended = 2
}
