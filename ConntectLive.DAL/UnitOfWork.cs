using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConntectLive.DAL;
public class UnitOfWork(ApplicationDbContext context, IServiceProvider serviceProvider) : IUnitOfWork
{
    private readonly ApplicationDbContext _context = context;
    public IGenericRepository<UserEntity> Users { get; set; } = serviceProvider.GetRequiredService<IGenericRepository<UserEntity>>();
    public IGenericRepository<QuestionEntity> Questions { get; set; } = serviceProvider.GetRequiredService<IGenericRepository<QuestionEntity>>();
    public IGenericRepository<SessionEntity> Sessions { get; set; } = serviceProvider.GetRequiredService<IGenericRepository<SessionEntity>>();

    public void Commit()
    {
        _context.SaveChanges();
    }
}




public class UnitOfWork<C>(C context, IServiceProvider serviceProvider) : IUnitOfWork<C>
    where C: DbContext
{
    private readonly C _context = context;
   
    public void Commit()
    {
        _context.SaveChanges();
    }
}

