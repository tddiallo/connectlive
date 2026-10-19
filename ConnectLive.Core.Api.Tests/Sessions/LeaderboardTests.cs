using ConnectLive.Core.Api.Sessions;

namespace ConnectLive.Core.Api.Tests.Sessions;

public class LeaderboardTests
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Carol = Guid.NewGuid();

    private static readonly Dictionary<Guid, string> Names = new()
    {
        [Alice] = "alice", [Bob] = "bob", [Carol] = "carol"
    };

    [Fact]
    public void Rank_OrdersByScoreDescending_AndNumbersFromOne()
    {
        var board = Leaderboard.Rank(new Dictionary<Guid, int> { [Alice] = 100, [Bob] = 300, [Carol] = 200 }, Names);

        Assert.Equal(new[] { "bob", "carol", "alice" }, board.Select(e => e.Nickname));
        Assert.Equal(new[] { 1, 2, 3 }, board.Select(e => e.Rank));
    }

    [Fact]
    public void Rank_GivesTiedPlayersTheSameRank_AndSkipsTheNext()
    {
        var board = Leaderboard.Rank(new Dictionary<Guid, int> { [Alice] = 200, [Bob] = 200, [Carol] = 100 }, Names);

        Assert.Equal(new[] { 1, 1, 3 }, board.Select(e => e.Rank));
    }

    [Fact]
    public void Rank_BreaksTiesAlphabetically_SoTheOrderIsStable()
    {
        var board = Leaderboard.Rank(new Dictionary<Guid, int> { [Bob] = 100, [Alice] = 100 }, Names);

        Assert.Equal(new[] { "alice", "bob" }, board.Select(e => e.Nickname));
    }

    [Fact]
    public void Rank_FallsBackToAPlaceholder_WhenTheUserIsUnknown()
    {
        var board = Leaderboard.Rank(new Dictionary<Guid, int> { [Guid.NewGuid()] = 50 }, Names);

        Assert.Equal("Unknown", board.Single().Nickname);
    }

    [Fact]
    public void Rank_IsEmpty_WhenNobodyAnswered() =>
        Assert.Empty(Leaderboard.Rank(new Dictionary<Guid, int>(), Names));
}
