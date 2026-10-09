using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ConnectLive.Domain.Exceptions;
using EF = Microsoft.EntityFrameworkCore;

namespace ConntectLive.DAL;

/// <summary>
/// Session de quiz live. Porte ses propres règles de transition (<see cref="Start"/>, <see cref="MoveNext"/>,
/// <see cref="End"/>) pour que l'état ne puisse pas être modifié de façon incohérente depuis l'extérieur.
/// Voir docs/SESSIONS.md pour la machine à états.
/// </summary>
[Table("sessions")]
[EF.Index(nameof(Code), IsUnique = true)]
public class SessionEntity : IEntity
{
    public const int MaxTitleLength = 100;

    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Code court saisi par les joueurs pour rejoindre la session.</summary>
    [Column("code")]
    public string Code { get; private set; }

    [Column("title")]
    public string Title { get; private set; }

    [Column("host-user-id")]
    public Guid HostUserId { get; private set; }

    [Column("status")]
    public SessionStatus Status { get; private set; }

    /// <summary>Index (à partir de 0) de la question courante ; -1 tant que la session n'a pas démarré.</summary>
    [Column("current-question-index")]
    public int CurrentQuestionIndex { get; private set; } = -1;

    [Column("created-at")]
    public DateTime CreatedAt { get; private set; }

    [Column("started-at")]
    public DateTime? StartedAt { get; private set; }

    [Column("ended-at")]
    public DateTime? EndedAt { get; private set; }

    /// <summary>Jeton de concurrence optimiste, incrémenté à chaque transition.</summary>
    [Column("version")]
    [ConcurrencyCheck]
    public int Version { get; private set; }

    public virtual List<SessionQuestionEntity> Questions { get; private set; } = new();

    /// <summary>Identifiant de la question courante, ou <c>null</c> hors d'une session en cours.</summary>
    [NotMapped]
    public Guid? CurrentQuestionId =>
        Status == SessionStatus.Running
            ? Questions.FirstOrDefault(q => q.Order == CurrentQuestionIndex)?.QuestionId
            : null;

    /// <summary>Crée une session en <see cref="SessionStatus.Lobby"/>.</summary>
    /// <exception cref="ArgumentException">Titre vide/trop long, aucune question, ou questions en double.</exception>
    public static SessionEntity Create(string title, Guid hostUserId, IReadOnlyList<Guid> questionIds, string code, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (title.Trim().Length > MaxTitleLength)
            throw new ArgumentException($"Title cannot exceed {MaxTitleLength} characters.", nameof(title));
        if (questionIds is null || questionIds.Count == 0)
            throw new ArgumentException("A session needs at least one question.", nameof(questionIds));
        if (questionIds.Distinct().Count() != questionIds.Count)
            throw new ArgumentException("Questions must be distinct.", nameof(questionIds));

        var session = new SessionEntity
        {
            Code = code,
            Title = title.Trim(),
            HostUserId = hostUserId,
            Status = SessionStatus.Lobby,
            CreatedAt = now
        };
        session.Questions = questionIds
            .Select((questionId, order) => new SessionQuestionEntity { SessionId = session.Id, QuestionId = questionId, Order = order })
            .ToList();
        return session;
    }

    /// <summary><c>Lobby → Running</c> sur la première question.</summary>
    /// <exception cref="ConflictException">La session n'est pas en <c>Lobby</c>.</exception>
    public void Start(DateTime now)
    {
        if (Status != SessionStatus.Lobby)
            throw new ConflictException($"Session cannot be started from status '{Status}'.");

        Status = SessionStatus.Running;
        CurrentQuestionIndex = 0;
        StartedAt = now;
        Version++;
    }

    /// <summary>Passe à la question suivante ; termine la session après la dernière.</summary>
    /// <returns><c>true</c> s'il reste une question courante, <c>false</c> si la session vient de se terminer.</returns>
    /// <exception cref="ConflictException">La session n'est pas en <c>Running</c>.</exception>
    public bool MoveNext(DateTime now)
    {
        if (Status != SessionStatus.Running)
            throw new ConflictException($"Cannot move to the next question from status '{Status}'.");

        if (CurrentQuestionIndex + 1 >= Questions.Count)
        {
            End(now);
            return false;
        }

        CurrentQuestionIndex++;
        Version++;
        return true;
    }

    /// <summary>Termine la session depuis <c>Lobby</c> ou <c>Running</c>.</summary>
    /// <exception cref="ConflictException">La session est déjà terminée.</exception>
    public void End(DateTime now)
    {
        if (Status == SessionStatus.Ended)
            throw new ConflictException("Session is already ended.");

        Status = SessionStatus.Ended;
        EndedAt = now;
        Version++;
    }
}
