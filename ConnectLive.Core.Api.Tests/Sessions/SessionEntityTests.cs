using ConnectLive.Domain.Exceptions;
using ConntectLive.DAL;

namespace ConnectLive.Core.Api.Tests.Sessions;

public class SessionEntityTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static SessionEntity NewSession(int questionCount = 2) =>
        SessionEntity.Create("Quiz culture", Guid.NewGuid(),
            Enumerable.Range(0, questionCount).Select(_ => Guid.NewGuid()).ToList(), "ABC234", Now);

    #region Create
    [Fact]
    public void Create_StartsInLobby_WithOrderedQuestionsAndNoCurrentQuestion()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var session = SessionEntity.Create("  Quiz  ", Guid.NewGuid(), ids, "ABC234", Now);

        Assert.Equal(SessionStatus.Lobby, session.Status);
        Assert.Equal("Quiz", session.Title);
        Assert.Equal(-1, session.CurrentQuestionIndex);
        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(ids, session.Questions.OrderBy(q => q.Order).Select(q => q.QuestionId));
        Assert.Equal(new[] { 0, 1, 2 }, session.Questions.OrderBy(q => q.Order).Select(q => q.Order));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenTitleIsBlank(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            SessionEntity.Create(title, Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }, "ABC234", Now));
    }

    [Fact]
    public void Create_Throws_WhenTitleIsTooLong()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionEntity.Create(new string('x', 101), Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }, "ABC234", Now));
    }

    [Fact]
    public void Create_Throws_WhenNoQuestion()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionEntity.Create("Quiz", Guid.NewGuid(), new List<Guid>(), "ABC234", Now));
    }

    [Fact]
    public void Create_Throws_WhenQuestionsAreDuplicated()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            SessionEntity.Create("Quiz", Guid.NewGuid(), new List<Guid> { id, id }, "ABC234", Now));
    }
    #endregion

    #region Start
    [Fact]
    public void Start_MovesToRunningOnFirstQuestion()
    {
        var session = NewSession();
        var versionBefore = session.Version;

        session.Start(Now.AddMinutes(1));

        Assert.Equal(SessionStatus.Running, session.Status);
        Assert.Equal(0, session.CurrentQuestionIndex);
        Assert.Equal(Now.AddMinutes(1), session.StartedAt);
        Assert.Equal(versionBefore + 1, session.Version);
    }

    [Fact]
    public void Start_Throws_WhenAlreadyRunning()
    {
        var session = NewSession();
        session.Start(Now);

        Assert.Throws<ConflictException>(() => session.Start(Now));
    }
    #endregion

    #region MoveNext
    [Fact]
    public void MoveNext_AdvancesToNextQuestion()
    {
        var session = NewSession(3);
        session.Start(Now);

        var hasMore = session.MoveNext(Now);

        Assert.True(hasMore);
        Assert.Equal(1, session.CurrentQuestionIndex);
        Assert.Equal(SessionStatus.Running, session.Status);
    }

    [Fact]
    public void MoveNext_EndsSession_AfterLastQuestion()
    {
        var session = NewSession(2);
        session.Start(Now);
        session.MoveNext(Now);

        var hasMore = session.MoveNext(Now.AddMinutes(5));

        Assert.False(hasMore);
        Assert.Equal(SessionStatus.Ended, session.Status);
        Assert.Equal(Now.AddMinutes(5), session.EndedAt);
    }

    [Fact]
    public void MoveNext_Throws_WhenSessionIsNotRunning()
    {
        Assert.Throws<ConflictException>(() => NewSession().MoveNext(Now));
    }
    #endregion

    #region End
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void End_EndsSession_FromLobbyOrRunning(bool started)
    {
        var session = NewSession();
        if (started) session.Start(Now);

        session.End(Now.AddMinutes(2));

        Assert.Equal(SessionStatus.Ended, session.Status);
        Assert.Equal(Now.AddMinutes(2), session.EndedAt);
    }

    [Fact]
    public void End_Throws_WhenAlreadyEnded()
    {
        var session = NewSession();
        session.End(Now);

        Assert.Throws<ConflictException>(() => session.End(Now));
    }
    #endregion

    [Fact]
    public void CurrentQuestionId_ReturnsQuestionAtCurrentIndex()
    {
        var session = NewSession(2);
        Assert.Null(session.CurrentQuestionId);

        session.Start(Now);
        session.MoveNext(Now);

        Assert.Equal(session.Questions.Single(q => q.Order == 1).QuestionId, session.CurrentQuestionId);
    }
}
