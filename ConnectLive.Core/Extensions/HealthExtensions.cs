using ConnectLive.Core.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConnectLive.Core.Api.Extensions;

/// <summary>
/// Sondes pour l'orchestrateur (Kubernetes, load balancer) : <c>/health/live</c> (le process répond, aucune dépendance)
/// et <c>/health/ready</c> (Postgres, et Redis s'il est configuré). Publiques, mais elles ne renvoient que
/// « Healthy »/« Unhealthy » : aucun détail interne.
/// </summary>
public static class HealthExtensions
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddConnectLiveHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var checks = services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("postgres", tags: new[] { ReadyTag });

        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString(CachingExtensions.RedisConnectionStringName)))
            checks.AddCheck<RedisHealthCheck>("redis", tags: new[] { ReadyTag });

        return services;
    }

    public static WebApplication MapConnectLiveHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        return app;
    }
}
