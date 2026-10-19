using ConnectLive.Core.Api.Realtime;

namespace ConnectLive.Core.Api.Tests.Realtime;

/// <summary>
/// Comportement attendu de toute implémentation de <see cref="ISessionPresenceTracker"/>. Les mêmes scénarios sont
/// exécutés sur l'implémentation mémoire (<see cref="InMemoryPresenceTrackerTests"/>) et sur l'implémentation Redis
/// (<see cref="RedisPresenceTrackerTests"/>) : elles doivent être interchangeables.
/// </summary>
public abstract class PresenceTrackerContract
{
    private readonly string _unique = Guid.NewGuid().ToString("N");

    protected abstract ISessionPresenceTracker CreateTracker();

    /// <summary>Identifiants propres au test (Redis est partagé entre les tests).</summary>
    protected string S(string name) => $"{name}-{_unique}";
    protected string C(string name) => $"{name}{_unique}";

    protected async Task DistinctUsers_WhenSameUserHasTwoConnections()
    {
        var tracker = CreateTracker();
        await tracker.JoinAsync(C("c1"), S("s1"), "alice");
        await tracker.JoinAsync(C("c2"), S("s1"), "alice");
        await tracker.JoinAsync(C("c3"), S("s1"), "bob");

        Assert.Equal(new[] { "alice", "bob" }, (await tracker.GetParticipantsAsync(S("s1"))).OrderBy(p => p));
    }

    protected async Task ScopedToTheSession()
    {
        var tracker = CreateTracker();
        await tracker.JoinAsync(C("c1"), S("s1"), "alice");
        await tracker.JoinAsync(C("c2"), S("s2"), "bob");

        Assert.Equal(new[] { "alice" }, await tracker.GetParticipantsAsync(S("s1")));
    }

    protected async Task Leave_ReturnsRemovedPresence_AndForgetsTheConnection()
    {
        var tracker = CreateTracker();
        await tracker.JoinAsync(C("c1"), S("s1"), "alice");

        var removed = await tracker.LeaveAsync(C("c1"));

        Assert.Equal(new SessionPresence(S("s1"), "alice"), removed);
        Assert.Empty(await tracker.GetParticipantsAsync(S("s1")));
    }

    protected async Task Leave_ReturnsNull_WhenConnectionIsUnknown() =>
        Assert.Null(await CreateTracker().LeaveAsync(C("ghost")));

    protected async Task Leave_OnlyRemovesThatConnection_NotTheUsersOtherTabs()
    {
        var tracker = CreateTracker();
        await tracker.JoinAsync(C("c1"), S("s1"), "alice");
        await tracker.JoinAsync(C("c2"), S("s1"), "alice");

        await tracker.LeaveAsync(C("c1"));

        Assert.Equal(new[] { "alice" }, await tracker.GetParticipantsAsync(S("s1")));
    }

    protected async Task Join_MovesConnectionToNewSession()
    {
        var tracker = CreateTracker();
        await tracker.JoinAsync(C("c1"), S("s1"), "alice");
        await tracker.JoinAsync(C("c1"), S("s2"), "alice");

        Assert.Empty(await tracker.GetParticipantsAsync(S("s1")));
        Assert.Equal(new[] { "alice" }, await tracker.GetParticipantsAsync(S("s2")));
    }

    protected async Task GetParticipants_ReturnsSnapshot()
    {
        var tracker = CreateTracker();
        await tracker.JoinAsync(C("c1"), S("s1"), "alice");
        var snapshot = await tracker.GetParticipantsAsync(S("s1"));

        await tracker.JoinAsync(C("c2"), S("s1"), "bob");

        Assert.Single(snapshot);
    }
}

public class InMemoryPresenceTrackerTests : PresenceTrackerContract
{
    protected override ISessionPresenceTracker CreateTracker() => new SessionPresenceTracker();

    [Fact] public Task Distinct_users() => DistinctUsers_WhenSameUserHasTwoConnections();
    [Fact] public Task Scoped_to_session() => ScopedToTheSession();
    [Fact] public Task Leave_returns_presence() => Leave_ReturnsRemovedPresence_AndForgetsTheConnection();
    [Fact] public Task Leave_unknown_returns_null() => Leave_ReturnsNull_WhenConnectionIsUnknown();
    [Fact] public Task Leave_keeps_other_tabs() => Leave_OnlyRemovesThatConnection_NotTheUsersOtherTabs();
    [Fact] public Task Join_moves_connection() => Join_MovesConnectionToNewSession();
    [Fact] public Task Snapshot() => GetParticipants_ReturnsSnapshot();
}
