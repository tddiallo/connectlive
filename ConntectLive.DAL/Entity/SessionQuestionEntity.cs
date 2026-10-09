using System.ComponentModel.DataAnnotations.Schema;

namespace ConntectLive.DAL;

/// <summary>Place d'une question dans l'ordre de passage d'une session.</summary>
[Table("session_questions")]
public class SessionQuestionEntity : IEntity
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("session-id")]
    public Guid SessionId { get; set; }

    [Column("question-id")]
    public Guid QuestionId { get; set; }

    /// <summary>Position à partir de 0.</summary>
    [Column("order")]
    public int Order { get; set; }
}
