using ConnectLive.Portal.Shared.Realtime;
using ConnectLive.Portal.Shared.Response.Sessions;
using ConntectLive.DAL;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Sessions;

/// <summary>Chargement et projections partagés par les handlers de sessions.</summary>
public static class SessionSupport
{
    /// <summary>Charge une session avec ses questions.</summary>
    /// <exception cref="KeyNotFoundException">Aucune session avec cet identifiant (→ HTTP 404).</exception>
    public static async Task<SessionEntity> GetSessionOrThrowAsync(this ApplicationDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.Sessions.Include(s => s.Questions).FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Session '{id}' cannot be found.");

    public static SessionResponse ToResponse(this SessionEntity session) => new()
    {
        Id = session.Id,
        Code = session.Code,
        Title = session.Title,
        Status = session.Status.ToString(),
        CurrentQuestionIndex = session.CurrentQuestionIndex,
        TotalQuestions = session.Questions.Count,
        CreatedAt = session.CreatedAt,
        StartedAt = session.StartedAt,
        EndedAt = session.EndedAt
    };

    /// <summary>
    /// Projette la question courante de la session vers sa vue joueur (sans la bonne réponse).
    /// </summary>
    public static async Task<SessionQuestionDto> ToCurrentQuestionDtoAsync(this ApplicationDbContext db, SessionEntity session, CancellationToken cancellationToken)
    {
        var questionId = session.CurrentQuestionId
            ?? throw new InvalidOperationException("The session has no current question.");
        var question = await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Question '{questionId}' of session '{session.Id}' cannot be found.");

        return new SessionQuestionDto(
            question.Id,
            question.Question,
            question.Category ?? string.Empty,
            (IReadOnlyList<string>)(question.Choices ?? new List<string>()),
            session.CurrentQuestionIndex,
            session.Questions.Count);
    }
}
