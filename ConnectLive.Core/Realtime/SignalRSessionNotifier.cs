using ConnectLive.Domain.Model;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace ConnectLive.Core.Api.Realtime;

/// <summary><see cref="ISessionNotifier"/> basé sur SignalR : un envoi vers le groupe de la session.</summary>
public sealed class SignalRSessionNotifier : ISessionNotifier
{
    private readonly IHubContext<SessionHub> _hubContext;

    public SignalRSessionNotifier(IHubContext<SessionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyQuestionChangedAsync(string sessionId, QuestionModel question, CancellationToken cancellationToken = default) =>
        SendToSession(sessionId, SessionHubEvents.QuestionChanged, question, cancellationToken);

    public Task NotifyLeaderboardUpdatedAsync(string sessionId, IReadOnlyList<LeaderboardModel> leaderboard, CancellationToken cancellationToken = default) =>
        SendToSession(sessionId, SessionHubEvents.LeaderboardUpdated, leaderboard, cancellationToken);

    public Task NotifySessionEndedAsync(string sessionId, CancellationToken cancellationToken = default) =>
        SendToSession(sessionId, SessionHubEvents.SessionEnded, sessionId, cancellationToken);

    private Task SendToSession(string sessionId, string method, object payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(SessionHub.GroupName(sessionId)).SendAsync(method, payload, cancellationToken);
}
