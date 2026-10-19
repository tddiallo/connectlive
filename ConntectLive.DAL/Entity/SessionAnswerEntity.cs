using System.ComponentModel.DataAnnotations.Schema;
using EF = Microsoft.EntityFrameworkCore;

namespace ConntectLive.DAL;

/// <summary>
/// Réponse d'un joueur à une question d'une session. L'index unique (session, question, joueur) garantit en base
/// qu'on ne peut répondre qu'une fois, même si deux requêtes arrivent en même temps.
/// </summary>
[Table("session_answers")]
[EF.Index(nameof(SessionId), nameof(QuestionId), nameof(UserId), IsUnique = true)]
public class SessionAnswerEntity : IEntity
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("session-id")]
    public Guid SessionId { get; set; }

    [Column("question-id")]
    public Guid QuestionId { get; set; }

    [Column("user-id")]
    public Guid UserId { get; set; }

    [Column("answer")]
    public string Answer { get; set; } = string.Empty;

    /// <summary>Résultat de la correction, figé au moment de la réponse (la question peut être modifiée ensuite).</summary>
    [Column("is-correct")]
    public bool IsCorrect { get; set; }

    [Column("points")]
    public int Points { get; set; }

    [Column("answered-at")]
    public DateTime AnsweredAt { get; set; }
}
