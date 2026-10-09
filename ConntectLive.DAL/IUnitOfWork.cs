namespace ConntectLive.DAL;
public interface IUnitOfWork
{
    void Commit();
    
    IGenericRepository<UserEntity> Users { get; set; }
    IGenericRepository<QuestionEntity> Questions { get; set; }
    IGenericRepository<SessionEntity> Sessions { get; set; }
}


public interface IUnitOfWork<C>
{
    void Commit();
}