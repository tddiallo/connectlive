using ConnectLive.Core.Api.Commands;
using ConnectLive.Core.Api.Queries;
using ConnectLive.Core.Api.Realtime;
using ConnectLive.Domain.Exceptions;
using ConnectLive.Portal.Shared.Realtime;
using ConnectLive.Portal.Shared.Request.Sessions;
using ConntectLive.DAL;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static ConnectLive.Core.Api.Commands.SessionCommands;
using static ConnectLive.Core.Api.Queries.SessionQueries;

namespace ConnectLive.Core.Api.Tests.Sessions;

/// <summary>Handlers de sessions testés sur un vrai SQLite en mémoire (mêmes contraintes que la base de dev).</summary>
public sealed class SessionHandlersTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ISessionNotifier _notifier = Substitute.For<ISessionNotifier>();
    private readonly ServiceProvider _services;
    private readonly List<QuestionEntity> _questions;

    public SessionHandlersTests()
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
        _questions = Enumerable.Range(1, 3).Select(i => new QuestionEntity
        {
            Question = $"Question {i} ?",
            Category = "Culture",
            Choices = new List<string> { "A", "B", "C", "D" },
            CorrectAnswer = "B"
        }).ToList();
        db.Questions.AddRange(_questions);
        db.SaveChanges();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private async Task<TResult> Run<TResult>(Func<IServiceProvider, Task<TResult>> action)
    {
        using var scope = _services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<Portal.Shared.Response.Sessions.SessionResponse> Create(int questionCount = 2) =>
        Run(sp => new CreateSessionCommandHandler(sp).Handle(
            new CreateSessionCommand(new CreateSessionRequest
            {
                Title = "Quiz",
                HostUserId = Guid.NewGuid(),
                QuestionIds = _questions.Take(questionCount).Select(q => q.Id).ToList()
            }), CancellationToken.None));

    #region Create
    [Fact]
    public async Task Create_PersistsSessionInLobby_WithGeneratedCode()
    {
        var created = await Create(3);

        Assert.Equal("Lobby", created.Status);
        Assert.Equal(6, created.Code.Length);
        Assert.Equal(3, created.TotalQuestions);
        Assert.Equal(-1, created.CurrentQuestionIndex);

        var fetched = await Run(sp => new GetSessionQueryHandler(sp).Handle(new GetSessionQuery(created.Id), CancellationToken.None));
        Assert.Equal(created.Code, fetched.Code);
    }

    [Fact]
    public async Task Create_Throws_WhenAQuestionDoesNotExist()
    {
        var request = new CreateSessionRequest
        {
            Title = "Quiz",
            QuestionIds = new List<Guid> { _questions[0].Id, Guid.NewGuid() }
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Run(sp => new CreateSessionCommandHandler(sp).Handle(new CreateSessionCommand(request), CancellationToken.None)));
    }
    #endregion

    #region Queries
    [Fact]
    public async Task GetByCode_IsCaseInsensitive()
    {
        var created = await Create();

        var found = await Run(sp => new GetSessionByCodeQueryHandler(sp).Handle(
            new GetSessionByCodeQuery(created.Code.ToLowerInvariant()), CancellationToken.None));

        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task GetById_Throws_WhenSessionDoesNotExist()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Run(sp => new GetSessionQueryHandler(sp).Handle(new GetSessionQuery(Guid.NewGuid()), CancellationToken.None)));
    }

    [Fact]
    public async Task GetByCode_Throws_WhenCodeIsUnknown()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Run(sp => new GetSessionByCodeQueryHandler(sp).Handle(new GetSessionByCodeQuery("ZZZZZZ"), CancellationToken.None)));
    }
    #endregion

    #region Lifecycle
    [Fact]
    public async Task Start_RunsSession_AndNotifiesFirstQuestionWithoutCorrectAnswer()
    {
        var created = await Create(2);

        var started = await Run(sp => new StartSessionCommandHandler(sp).Handle(new StartSessionCommand(created.Id), CancellationToken.None));

        Assert.Equal("Running", started.Status);
        Assert.Equal(0, started.CurrentQuestionIndex);
        await _notifier.Received(1).NotifyQuestionChangedAsync(created.Id.ToString(),
            Arg.Is<SessionQuestionDto>(q => q.QuestionId == _questions[0].Id && q.Index == 0 && q.Total == 2 &&
                                            q.Text == "Question 1 ?" && q.Choices.Count == 4),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_Throws_WhenAlreadyStarted_AndDoesNotNotifyAgain()
    {
        var created = await Create();
        await Run(sp => new StartSessionCommandHandler(sp).Handle(new StartSessionCommand(created.Id), CancellationToken.None));

        await Assert.ThrowsAsync<ConflictException>(() =>
            Run(sp => new StartSessionCommandHandler(sp).Handle(new StartSessionCommand(created.Id), CancellationToken.None)));

        await _notifier.Received(1).NotifyQuestionChangedAsync(Arg.Any<string>(), Arg.Any<SessionQuestionDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Next_NotifiesSecondQuestion_ThenEndsSessionAfterTheLastOne()
    {
        var created = await Create(2);
        await Run(sp => new StartSessionCommandHandler(sp).Handle(new StartSessionCommand(created.Id), CancellationToken.None));

        var second = await Run(sp => new NextQuestionCommandHandler(sp).Handle(new NextQuestionCommand(created.Id), CancellationToken.None));
        var ended = await Run(sp => new NextQuestionCommandHandler(sp).Handle(new NextQuestionCommand(created.Id), CancellationToken.None));

        Assert.Equal(1, second.CurrentQuestionIndex);
        Assert.Equal("Ended", ended.Status);
        await _notifier.Received(1).NotifyQuestionChangedAsync(created.Id.ToString(),
            Arg.Is<SessionQuestionDto>(q => q.QuestionId == _questions[1].Id && q.Index == 1), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifySessionEndedAsync(created.Id.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Next_Throws_WhenSessionIsInLobby()
    {
        var created = await Create();

        await Assert.ThrowsAsync<ConflictException>(() =>
            Run(sp => new NextQuestionCommandHandler(sp).Handle(new NextQuestionCommand(created.Id), CancellationToken.None)));
    }

    [Fact]
    public async Task End_EndsSession_AndNotifies()
    {
        var created = await Create();

        var ended = await Run(sp => new EndSessionCommandHandler(sp).Handle(new EndSessionCommand(created.Id), CancellationToken.None));

        Assert.Equal("Ended", ended.Status);
        Assert.NotNull(ended.EndedAt);
        await _notifier.Received(1).NotifySessionEndedAsync(created.Id.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commit_Throws_WhenTheSessionWasModifiedByAnotherWriter()
    {
        var created = await Create(3);
        await Run(sp => new StartSessionCommandHandler(sp).Handle(new StartSessionCommand(created.Id), CancellationToken.None));

        using var scopeA = _services.CreateScope();
        using var scopeB = _services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sessionA = await dbA.Sessions.Include(s => s.Questions).SingleAsync(s => s.Id == created.Id);
        var sessionB = await dbB.Sessions.Include(s => s.Questions).SingleAsync(s => s.Id == created.Id);

        sessionA.MoveNext(DateTime.UtcNow);
        await dbA.SaveChangesAsync();
        sessionB.MoveNext(DateTime.UtcNow);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    [Fact]
    public async Task Start_Throws_WhenSessionDoesNotExist()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Run(sp => new StartSessionCommandHandler(sp).Handle(new StartSessionCommand(Guid.NewGuid()), CancellationToken.None)));
    }
    #endregion
}
