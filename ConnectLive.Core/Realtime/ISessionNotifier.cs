using ConnectLive.Domain.Model;

namespace ConnectLive.Core.Api.Realtime;

/// <summary>
/// Pousse des événements temps réel aux participants d'une session. Les handlers MediatR dépendent de cette
/// abstraction plutôt que de SignalR, ce qui les garde testables et indépendants du transport.
/// </summary>
public interface ISessionNotifier
{
    /// <summary>Annonce la nouvelle question courante.</summary>
    Task NotifyQuestionChangedAsync(string sessionId, QuestionModel question, CancellationToken cancellationToken = default);

    /// <summary>Diffuse le classement à jour.</summary>
    Task NotifyLeaderboardUpdatedAsync(string sessionId, IReadOnlyList<LeaderboardModel> leaderboard, CancellationToken cancellationToken = default);

    /// <summary>Annonce la fin de la session.</summary>
    Task NotifySessionEndedAsync(string sessionId, CancellationToken cancellationToken = default);
}
