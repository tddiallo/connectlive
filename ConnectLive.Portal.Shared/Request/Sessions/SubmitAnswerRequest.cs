using System.ComponentModel.DataAnnotations;

namespace ConnectLive.Portal.Shared.Request.Sessions;

/// <summary>Réponse d'un joueur à la question courante.</summary>
public class SubmitAnswerRequest
{
    public const int MaxAnswerLength = 200;

    /// <summary>Question à laquelle le joueur répond : doit être la question courante (évite de répondre à la mauvaise après un changement).</summary>
    [Required]
    public Guid QuestionId { get; set; }

    /// <summary>Un des choix proposés, tel qu'envoyé dans <c>QuestionChanged</c>.</summary>
    [Required, StringLength(MaxAnswerLength, MinimumLength = 1)]
    public string Answer { get; set; } = string.Empty;
}
