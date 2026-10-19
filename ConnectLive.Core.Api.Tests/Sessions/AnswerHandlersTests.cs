using ConnectLive.Core.Api.Commands;
using ConnectLive.Core.Api.Queries;
using ConnectLive.Core.Api.Realtime;
using ConnectLive.Domain.Exceptions;
using ConnectLive.Portal.Shared.Realtime;
using ConnectLive.Portal.Shared.Request.Sessions;
using ConnectLive.Portal.Shared.Response.Sessions;
using ConntectLive.DAL;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static ConnectLive.Core.Api.Commands.SessionCommands;
using static ConnectLive.Core.Api.Queries.SessionQueries;

namespace ConnectLive.Core.Api.Tests.Sessions;

/// <summary>Réponses des joueurs : correction côté serveur, règles d'éligibilité et classement diffusé.</summary>
public sealed class AnswerHandlersTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ISessionNotifier _notifier = Substitute.For<ISessionNotifier>();
    private readonly ServiceProvider _services;
    private readonly List<QuestionEntity> _questions;
    private readonly Guid _host = Guid.NewGuid();
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    public AnswerHandlersTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection))
            .AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddSingleton(_notifier)
            .AddSingleton(TimeProvider.System)
            .BuildServiceProvider();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        _questions = Enumerable.Range(1, 2).Select(i => new QuestionEntity
        {
            Question = $"Question {i} ?",
            Category = "Culture",
            Choices = new List<string> { "A", "B", "C", "D" },
            CorrectAnswer = "B"
        }).ToList();
        db.Questions.AddRange(_questions);
        db.Users.AddRange(
            new UserEntity { Id = _alice, FirstName = "Alice", LastName = "A", Email = "a@x.com", Nickname = "alice" },
            new UserEntity { Id = _bob, FirstName = "Bob", LastName = "B", Email = "b@x.com", Nickname = "bob" });
        db.SaveChanges();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private async Task<T> Run<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private async Task<SessionResponse> RunningSession()
    {
        var created = await Run(sp => new CreateSessionCommandHandler(sp).Handle(
            new CreateSessionCommand(new CreateSessionRequest { Title = "Quiz", QuestionIds = _questions.Select(q => q.Id).ToList() }, _host),
            CancellationToken.None));
        await Run(sp => new StartSessionCommandHandler(sp).Handle(new StartSessionCommand(created.Id, _host), CancellationToken.None));
        return created;
    }

    private Task<AnswerResultResponse> Answer(Guid sessionId, Guid user, string answer, int questionIndex = 0) =>
        Run(sp => new SubmitAnswerCommandHandler(sp).Handle(
            new SubmitAnswerCommand(sessionId, user, new SubmitAnswerRequest { QuestionId = _questions[questionIndex].Id, Answer = answer }),
            CancellationToken.None));

    [Fact]
    public async Task CorrectAnswer_ScoresPoints_AndReportsTheTotal()
    {
        var session = await RunningSession();

        var result = await Answer(session.Id, _alice, "B");

        Assert.True(result.IsCorrect);
        Assert.Equal(SubmitAnswerCommandHandler.PointsPerCorrectAnswer, result.Points);
        Assert.Equal(SubmitAnswerCommandHandler.PointsPerCorrectAnswer, result.TotalScore);
    }

    [Fact]
    public async Task WrongAnswer_ScoresNothing()
    {
        var session = await RunningSession();

        var result = await Answer(session.Id, _alice, "A");

        Assert.False(result.IsCorrect);
        Assert.Equal(0, result.Points);
    }

    [Fact]
    public async Task Answer_CanOnlyBeSubmittedOnce_PerPlayerAndQuestion()
    {
        var session = await RunningSession();
        await Answer(session.Id, _alice, "A");

        await Assert.ThrowsAsync<ConflictException>(() => Answer(session.Id, _alice, "B"));
    }

    [Fact]
    public async Task Answer_IsRejected_ForAChoiceThatWasNotProposed()
    {
        var session = await RunningSession();

        await Assert.ThrowsAsync<ArgumentException>(() => Answer(session.Id, _alice, "Z"));
    }

    [Fact]
    public async Task Answer_IsRejected_ForAQuestionThatIsNotTheCurrentOne()
    {
        var session = await RunningSession();

        await Assert.ThrowsAsync<ConflictException>(() => Answer(session.Id, _alice, "B", questionIndex: 1));
    }

    [Fact]
    public async Task Answer_IsRejected_WhenTheSessionIsNotRunning()
    {
        var created = await Run(sp => new CreateSessionCommandHandler(sp).Handle(
            new CreateSessionCommand(new CreateSessionRequest { Title = "Quiz", QuestionIds = _questions.Select(q => q.Id).ToList() }, _host),
            CancellationToken.None));

        await Assert.ThrowsAsync<ConflictException>(() => Answer(created.Id, _alice, "B"));
    }

    [Fact]
    public async Task TheHost_CannotPlayTheirOwnSession()
    {
        var session = await RunningSession();

        await Assert.ThrowsAsync<ForbiddenException>(() => Answer(session.Id, _host, "B"));
    }

    [Fact]
    public async Task Answer_BroadcastsTheRankedLeaderboard()
    {
        var session = await RunningSession();
        await Answer(session.Id, _alice, "B");

        await Answer(session.Id, _bob, "A");

        await _notifier.Received(1).NotifyLeaderboardUpdatedAsync(session.Id.ToString(),
            Arg.Is<IReadOnlyList<LeaderboardEntryDto>>(l =>
                l.Count == 2 &&
                l[0].Nickname == "alice" && l[0].Rank == 1 && l[0].Score == SubmitAnswerCommandHandler.PointsPerCorrectAnswer &&
                l[1].Nickname == "bob" && l[1].Rank == 2 && l[1].Score == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leaderboard_SumsPointsAcrossQuestions()
    {
        var session = await RunningSession();
        await Answer(session.Id, _alice, "B");
        await Run(sp => new NextQuestionCommandHandler(sp).Handle(new NextQuestionCommand(session.Id, _host), CancellationToken.None));
        await Answer(session.Id, _alice, "B", questionIndex: 1);

        var board = await Run(sp => new GetLeaderboardQueryHandler(sp).Handle(new GetLeaderboardQuery(session.Id), CancellationToken.None));

        Assert.Equal(2 * SubmitAnswerCommandHandler.PointsPerCorrectAnswer, board.Single().Score);
    }

    [Fact]
    public async Task Leaderboard_Throws_WhenSessionDoesNotExist()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Run(sp => new GetLeaderboardQueryHandler(sp).Handle(new GetLeaderboardQuery(Guid.NewGuid()), CancellationToken.None)));
    }
}
