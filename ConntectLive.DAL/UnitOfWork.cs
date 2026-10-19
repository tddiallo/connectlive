using Microsoft.Extensions.DependencyInjection;

namespace ConntectLive.DAL;

public class UnitOfWork(ApplicationDbContext context, IServiceProvider serviceProvider) : IUnitOfWork
{
    private readonly ApplicationDbContext _context = context;

    public IGenericRepository<UserEntity> Users { get; } = serviceProvider.GetRequiredService<IGenericRepository<UserEntity>>();
    public IGenericRepository<QuestionEntity> Questions { get; } = serviceProvider.GetRequiredService<IGenericRepository<QuestionEntity>>();
    public IGenericRepository<SessionEntity> Sessions { get; } = serviceProvider.GetRequiredService<IGenericRepository<SessionEntity>>();
    public IGenericRepository<SessionAnswerEntity> SessionAnswers { get; } = serviceProvider.GetRequiredService<IGenericRepository<SessionAnswerEntity>>();

    public Task CommitAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
