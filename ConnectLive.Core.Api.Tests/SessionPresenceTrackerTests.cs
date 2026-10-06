using ConnectLive.Core.Api.Realtime;

namespace ConnectLive.Core.Api.Tests;

public class SessionPresenceTrackerTests
{
    [Fact]
    public void GetParticipants_ReturnsDistinctUsers_WhenSameUserHasTwoConnections()
    {
        var tracker = new SessionPresenceTracker();
        tracker.Join("c1", "s1", "alice");
        tracker.Join("c2", "s1", "alice");
        tracker.Join("c3", "s1", "bob");

        var participants = tracker.GetParticipants("s1");

        Assert.Equal(new[] { "alice", "bob" }, participants.OrderBy(p => p));
    }

    [Fact]
    public void GetParticipants_IsScopedToTheSession()
    {
        var tracker = new SessionPresenceTracker();
        tracker.Join("c1", "s1", "alice");
        tracker.Join("c2", "s2", "bob");

        Assert.Equal(new[] { "alice" }, tracker.GetParticipants("s1"));
    }

    [Fact]
    public void Leave_ReturnsRemovedPresence_AndForgetsTheConnection()
    {
        var tracker = new SessionPresenceTracker();
        tracker.Join("c1", "s1", "alice");

        var removed = tracker.Leave("c1");

        Assert.Equal(new SessionPresence("s1", "alice"), removed);
        Assert.Empty(tracker.GetParticipants("s1"));
    }

    [Fact]
    public void Leave_ReturnsNull_WhenConnectionIsUnknown()
    {
        Assert.Null(new SessionPresenceTracker().Leave("ghost"));
    }

    [Fact]
    public void Join_MovesConnectionToNewSession_WhenItJoinsAnotherOne()
    {
        var tracker = new SessionPresenceTracker();
        tracker.Join("c1", "s1", "alice");
        tracker.Join("c1", "s2", "alice");

        Assert.Empty(tracker.GetParticipants("s1"));
        Assert.Equal(new[] { "alice" }, tracker.GetParticipants("s2"));
    }

    [Fact]
    public void GetParticipants_ReturnsSnapshot_NotAffectedByLaterJoins()
    {
        var tracker = new SessionPresenceTracker();
        tracker.Join("c1", "s1", "alice");
        var snapshot = tracker.GetParticipants("s1");

        tracker.Join("c2", "s1", "bob");

        Assert.Single(snapshot);
    }
}
