namespace ConnectLive.Core.Api.Extensions;

/// <summary>
/// Enregistrement du cache distribué utilisé par <c>CacheBehavior</c>.
/// </summary>
public static class CachingExtensions
{
    /// <summary>Clé de configuration de la chaîne de connexion Redis (<c>ConnectionStrings:Redis</c>).</summary>
    public const string RedisConnectionStringName = "Redis";

    /// <summary>Préfixe appliqué à toutes les clés Redis de l'application.</summary>
    public const string RedisInstanceName = "ConnectLive:";

    /// <summary>
    /// Branche <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> sur Redis si
    /// <c>ConnectionStrings:Redis</c> est renseignée, sinon sur un cache mémoire de process.
    /// Le repli mémoire garde le développement local sans dépendance externe (même contrat, mais non partagé
    /// entre plusieurs instances de l'API).
    /// </summary>
    public static IServiceCollection AddConnectLiveCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString(RedisConnectionStringName);

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddDistributedMemoryCache();
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = RedisInstanceName;
        });
        return services;
    }
}
