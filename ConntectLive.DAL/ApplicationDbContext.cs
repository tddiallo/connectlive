using Microsoft.EntityFrameworkCore;

namespace ConntectLive.DAL;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base(options)
    {
    }

    //public virtual DbSet<LeaderboardEntity> Leaderboards { get; set; }
    //public virtual DbSet<QuestionEntity> Questions { get; set; }
    //public virtual DbSet<SessionEntity> Sessions { get; set; }
    public virtual DbSet<UserEntity> Users { get; set; }
    public virtual DbSet<QuestionEntity> Questions { get; set; }
    public virtual DbSet<SessionEntity> Sessions { get; set; }
    public virtual DbSet<SessionQuestionEntity> SessionQuestions { get; set; }
    public virtual DbSet<SessionAnswerEntity> SessionAnswers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SessionEntity>(session =>
        {
            // Statut stocké en texte (lisible en base, robuste à un réordonnancement de l'enum)
            session.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            session.Property(s => s.Code).HasMaxLength(8);
            session.Property(s => s.Title).HasMaxLength(SessionEntity.MaxTitleLength);
            session.HasMany(s => s.Questions)
                .WithOne()
                .HasForeignKey(q => q.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Les réponses disparaissent avec leur session
        modelBuilder.Entity<SessionAnswerEntity>()
            .HasOne<SessionEntity>()
            .WithMany()
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Une position n'est occupée que par une question par session
        modelBuilder.Entity<SessionQuestionEntity>()
            .HasIndex(q => new { q.SessionId, q.Order })
            .IsUnique();
    }

}
