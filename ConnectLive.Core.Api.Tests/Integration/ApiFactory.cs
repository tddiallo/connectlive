using ConntectLive.DAL;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectLive.Core.Api.Tests.Integration;

/// <summary>
/// API complète en mémoire (vrais middlewares, authentification, contrôleurs) sur un SQLite en mémoire.
/// La clé JWT est fournie par variable d'environnement, comme en déploiement.
/// <see cref="Shared"/> crée une « instance » supplémentaire qui partage la base et Redis : c'est ce qui permet de
/// tester le backplane SignalR avec deux serveurs.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private readonly string? _redis;

    public ApiFactory() : this("DataSource=:memory:", null)
    {
    }

    private ApiFactory(string sqliteConnectionString, string? redis)
    {
        _redis = redis;
        _connection = new SqliteConnection(sqliteConnectionString);
        _connection.Open();
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-0123456789");
        // Les migrations ciblent Postgres : sur le SQLite des tests, le schéma est créé par EnsureCreated.
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "false");
    }

    /// <summary>Instance d'API qui partage la base SQLite nommée <paramref name="sharedDatabaseName"/> et le Redis donné.</summary>
    public static ApiFactory Shared(string sharedDatabaseName, string redis) =>
        new($"DataSource=file:{sharedDatabaseName}?mode=memory&cache=shared", redis);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_redis is not null)
            builder.UseSetting("ConnectionStrings:Redis", _redis);

        builder.ConfigureTestServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                IsOptionsConfigurationFor(d.ServiceType) ||
                d.ServiceType == typeof(DbContextOptions)).ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        });
    }

    /// <summary>EF Core 9 empile les actions de configuration (<c>IDbContextOptionsConfiguration</c>, type interne) : il faut aussi retirer celle de Program (Npgsql).</summary>
    private static bool IsOptionsConfigurationFor(Type serviceType) =>
        serviceType.IsGenericType && serviceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal) &&
        serviceType.GenericTypeArguments[0] == typeof(ApplicationDbContext);

    /// <summary>Crée le schéma et insère <paramref name="count"/> questions ; renvoie leurs identifiants.</summary>
    public List<Guid> SeedQuestions(int count)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        var questions = Enumerable.Range(1, count).Select(i => new QuestionEntity
        {
            Question = $"Question {i} ?",
            Category = "Culture",
            Choices = new List<string> { "A", "B", "C", "D" },
            CorrectAnswer = "B"
        }).ToList();
        db.Questions.AddRange(questions);
        db.SaveChanges();
        return questions.Select(q => q.Id).ToList();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
