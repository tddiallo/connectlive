using ConnectLive.Core.Api.Realtime;
using StackExchange.Redis;

namespace ConnectLive.Core.Api.Extensions;

/// <summary>Branchement du temps réel : SignalR, présence, notifier — avec backplane Redis si Redis est configuré.</summary>
public static class RealtimeExtensions
{
    /// <summary>Préfixe des canaux Redis du backplane, pour partager un Redis avec d'autres applications sans collision.</summary>
    public const string BackplaneChannelPrefix = "ConnectLive:";

    /// <summary>
    /// Avec <c>ConnectionStrings:Redis</c> : backplane SignalR (les messages d'une instance atteignent les clients
    /// connectés aux autres) <b>et</b> présence partagée dans Redis. Sans : tout reste en mémoire de process, ce qui
    /// ne convient qu'à une instance unique. Voir docs/SIGNALR-BACKPLANE-REDIS.md.
    /// </summary>
    public static IServiceCollection AddConnectLiveRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        var signalR = services.AddSignalR();
        var redis = configuration.GetConnectionString(CachingExtensions.RedisConnectionStringName);

        if (string.IsNullOrWhiteSpace(redis))
        {
            services.AddSingleton<ISessionPresenceTracker, SessionPresenceTracker>();
        }
        else
        {
            signalR.AddStackExchangeRedis(redis, options =>
                options.Configuration.ChannelPrefix = RedisChannel.Literal(BackplaneChannelPrefix));

            // Connexion paresseuse : Redis n'est contacté qu'au premier JoinSession, pas au démarrage de l'API.
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
            services.AddSingleton<ISessionPresenceTracker, RedisSessionPresenceTracker>();
        }

        services.AddSingleton<ISessionNotifier, SignalRSessionNotifier>();
        return services;
    }
}
