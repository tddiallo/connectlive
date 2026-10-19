using ConnectLive.Core.Api.Realtime;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace ConnectLive.Core.Api.Tests;

public class SessionHubTests
{
    private readonly ISessionPresenceTracker _tracker = new SessionPresenceTracker();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly IHubCallerClients _clients = Substitute.For<IHubCallerClients>();
    private readonly IClientProxy _groupProxy = Substitute.For<IClientProxy>();
    private readonly SessionHub _hub;

    public SessionHubTests()
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-1");
        context.UserIdentifier.Returns("alice");
        _clients.Group(Arg.Any<string>()).Returns(_groupProxy);

        _hub = new SessionHub(_tracker) { Context = context, Groups = _groups, Clients = _clients };
    }

    [Fact]
    public async Task JoinSession_AddsConnectionToGroup_AndBroadcastsUserJoined()
    {
        var participants = await _hub.JoinSession("abc-123");

        await _groups.Received(1).AddToGroupAsync("conn-1", SessionHub.GroupName("abc-123"), Arg.Any<CancellationToken>());
        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.UserJoined,
            Arg.Is<object?[]>(a => a.Length == 1 && (string)a[0]! == "alice"), Arg.Any<CancellationToken>());
        Assert.Equal(new[] { "alice" }, participants);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad id!")]
    public async Task JoinSession_Throws_WhenSessionIdIsInvalid(string sessionId)
    {
        await Assert.ThrowsAsync<HubException>(() => _hub.JoinSession(sessionId));

        await _groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task JoinSession_Throws_WhenTheConnectionHasNoIdentity()
    {
        var anonymousContext = Substitute.For<HubCallerContext>();
        anonymousContext.ConnectionId.Returns("conn-2");
        anonymousContext.UserIdentifier.Returns((string?)null);
        var hub = new SessionHub(_tracker) { Context = anonymousContext, Groups = _groups, Clients = _clients };

        await Assert.ThrowsAsync<HubException>(() => hub.JoinSession("abc-123"));

        await _groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task LeaveSession_RemovesFromGroup_AndBroadcastsUserLeft()
    {
        await _hub.JoinSession("abc-123");

        await _hub.LeaveSession("abc-123");

        await _groups.Received(1).RemoveFromGroupAsync("conn-1", SessionHub.GroupName("abc-123"), Arg.Any<CancellationToken>());
        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.UserLeft,
            Arg.Is<object?[]>(a => (string)a[0]! == "alice"), Arg.Any<CancellationToken>());
        Assert.Empty(await _tracker.GetParticipantsAsync("abc-123"));
    }

    [Fact]
    public async Task OnDisconnectedAsync_BroadcastsUserLeft_ForTrackedConnection()
    {
        await _hub.JoinSession("abc-123");

        await _hub.OnDisconnectedAsync(null);

        await _groupProxy.Received(1).SendCoreAsync(SessionHubEvents.UserLeft,
            Arg.Is<object?[]>(a => (string)a[0]! == "alice"), Arg.Any<CancellationToken>());
        Assert.Empty(await _tracker.GetParticipantsAsync("abc-123"));
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNothing_ForUntrackedConnection()
    {
        await _hub.OnDisconnectedAsync(null);

        await _groupProxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }
}
