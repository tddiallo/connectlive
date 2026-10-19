namespace ConnectLive.Portal.Shared.Response.Sessions;

/// <summary>Résultat de la réponse d'un joueur (visible par lui seul).</summary>
public class AnswerResultResponse
{
    public Guid QuestionId { get; set; }

    public bool IsCorrect { get; set; }

    /// <summary>Points gagnés pour cette réponse.</summary>
    public int Points { get; set; }

    /// <summary>Total du joueur sur la session après cette réponse.</summary>
    public int TotalScore { get; set; }
}
