namespace ConnectLive.Portal.Shared.Response.Sessions;

/// <summary>État public d'une session de quiz.</summary>
public class SessionResponse
{
    public Guid Id { get; set; }

    /// <summary>Code à saisir par les joueurs pour rejoindre.</summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary><c>Lobby</c>, <c>Running</c> ou <c>Ended</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Index de la question courante (-1 avant le démarrage).</summary>
    public int CurrentQuestionIndex { get; set; }

    public int TotalQuestions { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }
}
