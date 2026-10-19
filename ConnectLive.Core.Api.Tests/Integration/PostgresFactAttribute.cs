namespace ConnectLive.Core.Api.Tests.Integration;

/// <summary>
/// Test exécuté seulement si <c>CONNECTLIVE_TEST_POSTGRES</c> contient une chaîne de connexion vers un serveur Postgres
/// (le compte doit pouvoir créer des bases). En local : <c>docker compose up -d connectlive-db</c> puis
/// <c>CONNECTLIVE_TEST_POSTGRES="Host=localhost;Port=5433;Username=connectlive;Password=Password1"</c>.
/// La CI le renseigne toujours (service Postgres), donc ces tests y sont obligatoires.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "CONNECTLIVE_TEST_POSTGRES";

    public static string? ServerConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable);

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ServerConnectionString))
            Skip = $"Définir {EnvironmentVariable} pour exécuter les tests Postgres.";
    }
}
