namespace ConntectLive.DAL;

/// <summary>
/// Accès générique à une entité. Les écritures ne sont persistées qu'au <see cref="IUnitOfWork.CommitAsync"/>.
/// Pour les lectures filtrées, projetées ou paginées, utiliser <see cref="Query"/> (LINQ, traduit en SQL).
/// </summary>
public interface IGenericRepository<TEntity> where TEntity : class, IEntity
{
    Task<TEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Requête en lecture seule (sans suivi des modifications).</summary>
    IQueryable<TEntity> Query();

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}
