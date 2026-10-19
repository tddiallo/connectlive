using ConntectLive.DAL;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConnectLive.Core.Api.Tests.Integration;

/// <summary>
/// Valide le schéma sur le moteur réellement utilisé (Postgres) : migrations appliquées sur une base vierge, absence de
/// dérive entre le modèle et les migrations, types natifs et contraintes d'unicité. Chaque test crée puis supprime sa base.
/// </summary>
public sealed class PostgresMigrationTests : IAsyncLifetime
{
    private string _databaseName = string.Empty;
    private string _databaseConnection = string.Empty;

    public async Task InitializeAsync()
    {
        if (PostgresFactAttribute.ServerConnectionString is not { } server)
            return;

        _databaseName = $"connectlive_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(server);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();

        _databaseConnection = new NpgsqlConnectionStringBuilder(server) { Database = _databaseName }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_databaseName) || PostgresFactAttribute.ServerConnectionString is not { } server)
            return;

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(server);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_databaseConnection).Options);

    [PostgresFact]
    public async Task Migrations_ApplyOnAnEmptyDatabase_WithoutModelDrift()
    {
        await using var db = NewContext();

        // EF Core 9 lève PendingModelChangesWarning si le modèle a changé sans migration correspondante.
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [PostgresFact]
    public async Task Schema_RoundTripsNativeTypes_AndEnforcesUniqueness()
    {
        await using (var db = NewContext())
            await db.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        var question = new QuestionEntity
        {
            UserId = userId, Question = "Q ?", Category = "Culture",
            Choices = new List<string> { "A", "B", "C", "D" }, CorrectAnswer = "B"
        };
        var session = SessionEntity.Create("Quiz", userId, new[] { question.Id }, "ABC234", DateTime.UtcNow);

        await using (var db = NewContext())
        {
            db.Users.Add(new UserEntity { Id = userId, FirstName = "A", LastName = "B", Email = "a@x.com", Nickname = "a" });
            db.Questions.Add(question);
            db.Sessions.Add(session);
            db.SessionAnswers.Add(new SessionAnswerEntity
            {
                SessionId = session.Id, QuestionId = question.Id, UserId = userId, Answer = "B",
                IsCorrect = true, Points = 100, AnsweredAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var stored = await db.Questions.SingleAsync(q => q.Id == question.Id);
            Assert.Equal(new[] { "A", "B", "C", "D" }, stored.Choices); // text[] natif

            var duplicateAnswer = new SessionAnswerEntity
            {
                SessionId = session.Id, QuestionId = question.Id, UserId = userId, Answer = "A",
                IsCorrect = false, Points = 0, AnsweredAt = DateTime.UtcNow
            };
            db.SessionAnswers.Add(duplicateAnswer);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = NewContext())
        {
            db.Users.Add(new UserEntity { FirstName = "C", LastName = "D", Email = "a@x.com", Nickname = "c" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); // email unique
        }
    }

    [PostgresFact]
    public async Task Sessions_UseOptimisticConcurrency_OnPostgres()
    {
        await using (var db = NewContext())
        {
            await db.Database.MigrateAsync();
            var host = Guid.NewGuid();
            var q = new QuestionEntity { UserId = host, Question = "Q", Choices = new() { "A", "B", "C", "D" }, CorrectAnswer = "A" };
            db.Questions.Add(q);
            db.Sessions.Add(SessionEntity.Create("Quiz", host, new[] { q.Id }, "XYZ234", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        await using var dbA = NewContext();
        await using var dbB = NewContext();
        var a = await dbA.Sessions.Include(s => s.Questions).SingleAsync();
        var b = await dbB.Sessions.Include(s => s.Questions).SingleAsync();

        a.Start(DateTime.UtcNow);
        await dbA.SaveChangesAsync();
        b.Start(DateTime.UtcNow);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }
}
