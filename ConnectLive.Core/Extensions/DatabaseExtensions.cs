using ConntectLive.DAL;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Extensions;

public static class DatabaseExtensions
{
    /// <summary>Clé de configuration : applique les migrations EF au démarrage (défaut : oui en Development, non ailleurs).</summary>
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>
    /// Applique les migrations en attente. En production on préfère les appliquer dans une étape de déploiement
    /// dédiée (<c>dotnet ef database update</c> ou un script SQL) plutôt qu'au démarrage de chaque instance.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
    }
}
