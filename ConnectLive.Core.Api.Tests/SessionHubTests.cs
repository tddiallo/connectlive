using ConnectLive.Core.Api.Realtime;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace ConnectLive.Core.Api.Tests;

public class SessionHubTests
{
    private readonly SessionPresenceTracker _tracker = new();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly IClientProxy _groupProxy = Substitute.For<IClientProxy>();
    private readonly SessionHub _hub;

    public SessionHubTests()
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-1");
        _clients.Group(Arg.Any<string>()).Returns(_groupProxy);

        _hub = new SessionHub(_tracker) { Context = context, Groups = _groups, Clients = _clients };
    }

    [Fact]
    public async Task JoinSession_AddsConnectionToGroup_AndBroadcastsUserJoined()
    {
        var participants = await _hub.JoinSession("abc-123", "alice");

        await _groups.Received(1).AddToGroupAsync("conn-1", SessionHub.GroupName("abc-123"), Arg.Any<CancellationToken>());
        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.UserJoined,
            Arg.Is<object?[]>(a => a.Length == 1 && (string)a[0]! == "alice"), Arg.Any<CancellationToken>());
        Assert.Equal(new[] { "alice" }, participants);
    }

    [Theory]
    [InlineData("", "alice")]
    [InlineData("   ", "alice")]
    [InlineData("bad id!", "alice")]
    [InlineData("abc-123", "")]
    public async Task JoinSession_Throws_WhenArgumentsAreInvalid(string sessionId, string userId)
    {
        await Assert.ThrowsAsync<HubException>(() => _hub.JoinSession(sessionId, userId));

        await _groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task LeaveSession_RemovesFromGroup_AndBroadcastsUserLeft()
    {
        await _hub.JoinSession("abc-123", "alice");

        await _hub.LeaveSession("abc-123");

        await _groups.Received(1).RemoveFromGroupAsync("conn-1", SessionHub.GroupName("abc-123"), Arg.Any<CancellationToken>());
        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.UserLeft,
            Arg.Is<object?[]>(a => (string)a[0]! == "alice"), Arg.Any<CancellationToken>());
        Assert.Empty(_tracker.GetParticipants("abc-123"));
    }

    [Fact]
    public async Task OnDisconnectedAsync_BroadcastsUserLeft_ForTrackedConnection()
    {
        await _hub.JoinSession("abc-123", "alice");

        await _hub.OnDisconnectedAsync(null);

        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.UserLeft,
            Arg.Is<object?[]>(a => (string)a[0]! == "alice"), Arg.Any<CancellationToken>());
        Assert.Empty(_tracker.GetParticipants("abc-123"));
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNothing_ForUntrackedConnection()
    {
        await _hub.OnDisconnectedAsync(null);

        await _groupProxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }
}
