using ConnectLive.Portal.Shared.Realtime;

namespace ConnectLive.Core.Api.Realtime;

/// <summary>
/// Décorateur d'<see cref="ISessionNotifier"/> : une notification est envoyée <b>après</b> la persistance, donc son
/// échec (Redis indisponible, par exemple) ne doit pas faire croire à l'appelant que l'action a échoué alors que l'état
/// en base a déjà changé. L'erreur est journalisée ; les clients se resynchronisent via
/// <c>GET /Session/{id}</c> et <c>GET /Session/{id}/leaderboard</c>. L'annulation, elle, est propagée.
/// </summary>
public sealed class ResilientSessionNotifier : ISessionNotifier
{
    private readonly ISessionNotifier _inner;
    private readonly ILogger<ResilientSessionNotifier> _logger;

    public ResilientSessionNotifier(ISessionNotifier inner, ILogger<ResilientSessionNotifier> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public Task NotifyQuestionChangedAsync(string sessionId, SessionQuestionDto question, CancellationToken cancellationToken = default) =>
        SafelyAsync(sessionId, nameof(NotifyQuestionChangedAsync), () => _inner.NotifyQuestionChangedAsync(sessionId, question, cancellationToken));

    public Task NotifyLeaderboardUpdatedAsync(string sessionId, IReadOnlyList<LeaderboardEntryDto> leaderboard, CancellationToken cancellationToken = default) =>
        SafelyAsync(sessionId, nameof(NotifyLeaderboardUpdatedAsync), () => _inner.NotifyLeaderboardUpdatedAsync(sessionId, leaderboard, cancellationToken));

    public Task NotifySessionEndedAsync(string sessionId, CancellationToken cancellationToken = default) =>
        SafelyAsync(sessionId, nameof(NotifySessionEndedAsync), () => _inner.NotifySessionEndedAsync(sessionId, cancellationToken));

    private async Task SafelyAsync(string sessionId, string operation, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "{Operation} failed for session {SessionId}; clients must resynchronize.", operation, sessionId);
        }
    }
}
