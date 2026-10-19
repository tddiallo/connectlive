using ConnectLive.Core.Api.Realtime;
using StackExchange.Redis;

namespace ConnectLive.Core.Api.Tests.Realtime;

/// <summary>
/// Présence partagée via Redis. Exécuté seulement si <c>CONNECTLIVE_TEST_REDIS</c> contient une chaîne de connexion
/// (ex. <c>localhost:6379</c>) ; la CI la renseigne toujours.
/// </summary>
public sealed class RedisPresenceTrackerTests : PresenceTrackerContract, IDisposable
{
    public const string EnvironmentVariable = "CONNECTLIVE_TEST_REDIS";

    private readonly List<IConnectionMultiplexer> _connections = new();

    public static string? ConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable);

    protected override ISessionPresenceTracker CreateTracker() => new RedisSessionPresenceTracker(Connect());

    private IConnectionMultiplexer Connect()
    {
        var connection = ConnectionMultiplexer.Connect(ConnectionString!);
        _connections.Add(connection);
        return connection;
    }

    public void Dispose()
    {
        foreach (var connection in _connections)
            connection.Dispose();
    }

    [RedisFact] public Task Distinct_users() => DistinctUsers_WhenSameUserHasTwoConnections();
    [RedisFact] public Task Scoped_to_session() => ScopedToTheSession();
    [RedisFact] public Task Leave_returns_presence() => Leave_ReturnsRemovedPresence_AndForgetsTheConnection();
    [RedisFact] public Task Leave_unknown_returns_null() => Leave_ReturnsNull_WhenConnectionIsUnknown();
    [RedisFact] public Task Leave_keeps_other_tabs() => Leave_OnlyRemovesThatConnection_NotTheUsersOtherTabs();
    [RedisFact] public Task Join_moves_connection() => Join_MovesConnectionToNewSession();
    [RedisFact] public Task Snapshot() => GetParticipants_ReturnsSnapshot();

    /// <summary>Deux « instances d'API » (deux trackers, deux connexions) voient la même présence : c'est l'intérêt de Redis.</summary>
    [RedisFact]
    public async Task TwoInstances_ShareTheSamePresence()
    {
        var instanceA = new RedisSessionPresenceTracker(Connect());
        var instanceB = new RedisSessionPresenceTracker(Connect());

        await instanceA.JoinAsync(C("on-a"), S("shared"), "alice");
        await instanceB.JoinAsync(C("on-b"), S("shared"), "bob");

        Assert.Equal(new[] { "alice", "bob" }, (await instanceA.GetParticipantsAsync(S("shared"))).OrderBy(p => p));
        // Une déconnexion constatée par l'instance B (ex. alice reconnectée là) est vue par A.
        await instanceB.LeaveAsync(C("on-a"));
        Assert.Equal(new[] { "bob" }, await instanceA.GetParticipantsAsync(S("shared")));
    }
}

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(RedisPresenceTrackerTests.ConnectionString))
            Skip = $"Définir {RedisPresenceTrackerTests.EnvironmentVariable} pour exécuter les tests Redis.";
    }
}
