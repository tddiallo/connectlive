namespace ConnectLive.Core.Api.Extensions;

/// <summary>Politique CORS pilotée par la configuration (<c>Security:AllowedOrigins</c>).</summary>
public static class CorsExtensions
{
    public const string PolicyName = "CorsPolicy";

    /// <summary>Clé de configuration : origines autorisées, séparées par des <c>;</c>.</summary>
    public const string AllowedOriginsKey = "Security:AllowedOrigins";

    /// <summary>Extrait la liste d'origines (séparées par <c>;</c> ou <c>,</c>), sans vides ni espaces ni slash final.</summary>
    public static string[] ParseOrigins(string? raw) =>
        (raw ?? string.Empty)
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// Autorise uniquement les origines configurées, avec les identifiants (requis par SignalR pour le jeton et
    /// les cookies). Sans configuration : tout est autorisé en Development (sans identifiants), et <b>rien</b> hors
    /// Development, pour qu'un oubli de configuration n'ouvre pas l'API à tous les sites.
    /// </summary>
    public static IServiceCollection AddConnectLiveCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = ParseOrigins(configuration[AllowedOriginsKey]);

        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            if (origins.Length > 0)
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            else if (environment.IsDevelopment())
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            // Sinon : aucune origine autorisée (fail closed).
        }));

        return services;
    }
}
