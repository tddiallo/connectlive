using Microsoft.EntityFrameworkCore;

namespace ConntectLive.DAL;

public class GenericRepository<TEntity>(ApplicationDbContext dbContext) : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public Task<TEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public IQueryable<TEntity> Query() => _dbContext.Set<TEntity>().AsNoTracking();

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => _dbContext.Set<TEntity>().Update(entity);

    public void Remove(TEntity entity)
    {
        if (_dbContext.Entry(entity).State == EntityState.Detached)
            _dbContext.Attach(entity);
        _dbContext.Set<TEntity>().Remove(entity);
    }
}
