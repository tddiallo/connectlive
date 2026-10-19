namespace ConntectLive.DAL;

/// <summary>Regroupe les repositories d'une requête et persiste leurs modifications en une seule transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>Persiste toutes les modifications en attente (un seul <c>SaveChanges</c>, donc une seule transaction).</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    IGenericRepository<UserEntity> Users { get; }
    IGenericRepository<QuestionEntity> Questions { get; }
    IGenericRepository<SessionEntity> Sessions { get; }
    IGenericRepository<SessionAnswerEntity> SessionAnswers { get; }
}
