using ConnectLive.Application.BusEvents;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectLive.Application.Messaging;

/// <summary>
/// Configuration unique de MassTransit/RabbitMQ pour tous les services (voir docs/MASSTRANSIT-FLUX.md).
/// Un service <b>publie</b> (Core.Api) ou <b>publie et consomme</b> (Notification.Api) selon <c>consume</c>.
/// </summary>
public static class MessagingExtensions
{
    /// <summary>Clés de configuration (inchangées par rapport à l'ancienne configuration).</summary>
    public const string ConnectionKey = "EventBusConnection";
    public const string UserNameKey = "EventBusUserName";
    public const string PasswordKey = "EventBusPassword";

    /// <summary>File d'attente du consommateur des événements « utilisateur créé ».</summary>
    public const string UserCreatedQueue = "users.service";

    /// <summary>Tentatives de traitement d'un message avant son envoi dans la file <c>users.service_error</c>.</summary>
    public const int ConsumerRetryCount = 5;

    /// <summary><c>true</c> si un broker est configuré (<c>EventBusConnection</c> renseignée).</summary>
    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[ConnectionKey]);

    /// <summary>
    /// Enregistre MassTransit sur RabbitMQ. Sans <c>EventBusConnection</c>, ne fait rien : l'application tourne
    /// sans broker et les publications sont simplement ignorées.
    /// </summary>
    /// <param name="consume">Ajoute le consommateur <see cref="UserCreatedConsumer"/> sur la file <see cref="UserCreatedQueue"/>.</param>
    /// <returns><c>true</c> si le bus a été enregistré.</returns>
    public static bool AddConnectLiveMessaging(this IServiceCollection services, IConfiguration configuration, bool consume)
    {
        if (!IsConfigured(configuration))
            return false;

        services.AddMassTransit(x =>
        {
            if (consume)
                x.AddConsumer<UserCreatedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(configuration[ConnectionKey]!), host =>
                {
                    host.Username(configuration[UserNameKey]);
                    host.Password(configuration[PasswordKey]);
                });

                if (consume)
                {
                    cfg.ReceiveEndpoint(UserCreatedQueue, endpoint =>
                    {
                        // Réessaie avec attente croissante (1 s, 2 s, 4 s…) avant de basculer le message en file « _error ».
                        endpoint.UseMessageRetry(retry => retry.Exponential(
                            ConsumerRetryCount, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1)));
                        endpoint.ConfigureConsumer<UserCreatedConsumer>(context);
                    });
                }
            });
        });

        return true;
    }
}
