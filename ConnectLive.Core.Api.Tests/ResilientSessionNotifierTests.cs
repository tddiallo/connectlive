using ConnectLive.Core.Api.Realtime;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ConnectLive.Core.Api.Tests;

/// <summary>Un échec d'envoi temps réel après la persistance ne doit pas transformer une action réussie en erreur 500.</summary>
public class ResilientSessionNotifierTests
{
    private readonly ISessionNotifier _inner = Substitute.For<ISessionNotifier>();
    private readonly ResilientSessionNotifier _notifier;

    public ResilientSessionNotifierTests()
    {
        _notifier = new ResilientSessionNotifier(_inner, NullLogger<ResilientSessionNotifier>.Instance);
    }

    private static SessionQuestionDto Question() => new(Guid.NewGuid(), "Q ?", "", new[] { "A", "B" }, 0, 1);

    [Fact]
    public async Task Forwards_ToTheInnerNotifier_WhenItWorks()
    {
        await _notifier.NotifyQuestionChangedAsync("s1", Question());
        await _notifier.NotifySessionEndedAsync("s1");
        await _notifier.NotifyLeaderboardUpdatedAsync("s1", new List<LeaderboardEntryDto>());

        await _inner.Received(1).NotifyQuestionChangedAsync("s1", Arg.Any<SessionQuestionDto>(), Arg.Any<CancellationToken>());
        await _inner.Received(1).NotifySessionEndedAsync("s1", Arg.Any<CancellationToken>());
        await _inner.Received(1).NotifyLeaderboardUpdatedAsync("s1", Arg.Any<IReadOnlyList<LeaderboardEntryDto>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwallowsFailures_ForEveryEventKind()
    {
        _inner.NotifyQuestionChangedAsync(default!, default!, default).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("redis down")));
        _inner.NotifySessionEndedAsync(default!, default).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("redis down")));
        _inner.NotifyLeaderboardUpdatedAsync(default!, default!, default).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("redis down")));

        await _notifier.NotifyQuestionChangedAsync("s1", Question());
        await _notifier.NotifySessionEndedAsync("s1");
        await _notifier.NotifyLeaderboardUpdatedAsync("s1", new List<LeaderboardEntryDto>());
    }

    [Fact]
    public async Task PropagatesCancellation()
    {
        _inner.NotifySessionEndedAsync(default!, default).ReturnsForAnyArgs(Task.FromException(new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _notifier.NotifySessionEndedAsync("s1"));
    }
}
