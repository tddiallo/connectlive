using ConnectLive.Application.Messaging;
using ConnectLive.Domain.Contracts;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectLive.Core.Api.Tests.Integration;

/// <summary>
/// Trajet réel d'un message à travers RabbitMQ : publication → exchange → file <c>users.service</c> → consommateur.
/// Exécuté seulement si <c>CONNECTLIVE_TEST_RABBITMQ</c> contient l'URI du broker (ex. <c>rabbitmq://localhost:56720</c>,
/// identifiants dans <c>CONNECTLIVE_TEST_RABBITMQ_USER</c> / <c>_PASSWORD</c>, <c>guest/guest</c> par défaut). La CI le renseigne toujours.
/// </summary>
public sealed class RabbitMqTransportTests
{
    private const string EnvironmentVariable = "CONNECTLIVE_TEST_RABBITMQ";

    public sealed class RabbitMqFactAttribute : FactAttribute
    {
        public RabbitMqFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
                Skip = $"Définir {EnvironmentVariable} pour exécuter ce test.";
        }
    }

    private sealed class CapturingLogger : ILoggerProvider, ILogger
    {
        private readonly List<string> _messages = new();

        public IReadOnlyList<string> Messages { get { lock (_messages) return _messages.ToList(); } }

        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_messages) _messages.Add(formatter(state, exception));
        }
        public void Dispose() { }
    }

    [RabbitMqFact]
    public async Task PublishedUserCreated_IsDeliveredToTheConsumer_ThroughRabbitMq()
    {
        var capture = new CapturingLogger();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [MessagingExtensions.ConnectionKey] = Environment.GetEnvironmentVariable(EnvironmentVariable),
            [MessagingExtensions.UserNameKey] = Environment.GetEnvironmentVariable(EnvironmentVariable + "_USER") ?? "guest",
            [MessagingExtensions.PasswordKey] = Environment.GetEnvironmentVariable(EnvironmentVariable + "_PASSWORD") ?? "guest"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        Assert.True(services.AddConnectLiveMessaging(configuration, consume: true));
        await using var provider = services.BuildServiceProvider();
        var busControl = provider.GetRequiredService<IBusControl>();
        await busControl.StartAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
        try
        {
            var marker = $"Test-{Guid.NewGuid():N}";
            await provider.GetRequiredService<IPublishEndpoint>().Publish(
                new UserCreatedContractQuery { Email = "alice@example.com", FullName = marker });

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && !capture.Messages.Any(m => m.Contains(marker)))
                await Task.Delay(100);

            Assert.Contains(capture.Messages, m => m.Contains(marker));
        }
        finally
        {
            await busControl.StopAsync();
        }
    }
}
