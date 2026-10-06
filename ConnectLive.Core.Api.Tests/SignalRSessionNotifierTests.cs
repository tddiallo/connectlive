using ConnectLive.Core.Api.Realtime;
using ConnectLive.Domain.Model;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace ConnectLive.Core.Api.Tests;

public class SignalRSessionNotifierTests
{
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IClientProxy _groupProxy = Substitute.For<IClientProxy>();
    private readonly SignalRSessionNotifier _notifier;

    public SignalRSessionNotifierTests()
    {
        var hubContext = Substitute.For<IHubContext<SessionHub>>();
        hubContext.Clients.Returns(_clients);
        _clients.Group(SessionHub.GroupName("s1")).Returns(_groupProxy);
        _notifier = new SignalRSessionNotifier(hubContext);
    }

    [Fact]
    public async Task NotifyQuestionChangedAsync_SendsQuestionToSessionGroup()
    {
        var question = new QuestionModel { QuestionId = Guid.NewGuid(), Question = "2+2 ?" };

        await _notifier.NotifyQuestionChangedAsync("s1", question);

        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.QuestionChanged,
            Arg.Is<object?[]>(a => ReferenceEquals(a[0], question)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyLeaderboardUpdatedAsync_SendsEntriesToSessionGroup()
    {
        var entries = new List<LeaderboardModel> { new() { Rank = 1, Score = 10 } };

        await _notifier.NotifyLeaderboardUpdatedAsync("s1", entries);

        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.LeaderboardUpdated,
            Arg.Is<object?[]>(a => ReferenceEquals(a[0], entries)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifySessionEndedAsync_SendsSessionIdToSessionGroup()
    {
        await _notifier.NotifySessionEndedAsync("s1");

        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.SessionEnded,
            Arg.Is<object?[]>(a => (string)a[0]! == "s1"), Arg.Any<CancellationToken>());
    }
}
