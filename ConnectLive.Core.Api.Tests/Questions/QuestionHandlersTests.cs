using System.ComponentModel.DataAnnotations;
using ConnectLive.Core.Api.Commands;
using ConnectLive.Core.Api.Queries;
using ConnectLive.Portal.Shared.Request.Questions;
using ConntectLive.DAL;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ConnectLive.Core.Api.Commands.QuestionCommands;
using static ConnectLive.Core.Api.Queries.QuestionQueries;

namespace ConnectLive.Core.Api.Tests.Questions;

public sealed class QuestionHandlersTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;
    private readonly Guid _owner = Guid.NewGuid();

    public QuestionHandlersTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection))
            .AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .BuildServiceProvider();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private static CreateQuestionRequest Valid(string text = "Capitale du Sénégal ?") => new()
    {
        Question = text,
        Category = "Géographie",
        Choice1 = "Dakar",
        Choice2 = "Thiès",
        Choice3 = "Saint-Louis",
        Choice4 = "Kaolack",
        CorrectAnswer = "Dakar"
    };

    private async Task<T> Run<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private static bool IsValid(CreateQuestionRequest request, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        return Validator.TryValidateObject(request, new ValidationContext(request), results, true);
    }

    [Fact]
    public async Task Create_PersistsTheQuestion_ForItsOwner()
    {
        var response = await Run(sp => new CreateQuestionCommandHandler(sp).Handle(new CreateQuestionCommand(Valid(), _owner), CancellationToken.None));

        using var scope = _services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Questions.SingleAsync(q => q.Id == response.Id);
        Assert.Equal(_owner, stored.UserId);
        Assert.Equal(new[] { "Dakar", "Thiès", "Saint-Louis", "Kaolack" }, stored.Choices);
        Assert.Equal("Dakar", stored.CorrectAnswer);
        Assert.Equal("Géographie", stored.Category);
    }

    [Fact]
    public async Task Create_IgnoresAnIdChosenByTheClient()
    {
        var request = Valid();
        var clientId = request.Id;

        var response = await Run(sp => new CreateQuestionCommandHandler(sp).Handle(new CreateQuestionCommand(request, _owner), CancellationToken.None));

        Assert.NotEqual(clientId, response.Id);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyTheCallersQuestions()
    {
        await Run(sp => new CreateQuestionCommandHandler(sp).Handle(new CreateQuestionCommand(Valid("Mine ?"), _owner), CancellationToken.None));
        await Run(sp => new CreateQuestionCommandHandler(sp).Handle(new CreateQuestionCommand(Valid("Theirs ?"), Guid.NewGuid()), CancellationToken.None));

        var mine = await Run(sp => new GetMyQuestionsQueryHandler(sp).Handle(new GetMyQuestionsQuery(_owner), CancellationToken.None));

        Assert.Equal("Mine ?", mine.Single().Question);
        Assert.Equal("Dakar", mine.Single().CorrectAnswer);
    }

    [Fact]
    public void Request_IsValid_WhenComplete() => Assert.True(IsValid(Valid(), out _));

    [Fact]
    public void Request_IsInvalid_WhenTheCorrectAnswerIsNotOneOfTheChoices()
    {
        var request = Valid();
        request.CorrectAnswer = "Paris";

        Assert.False(IsValid(request, out _));
    }

    [Fact]
    public void Request_IsInvalid_WhenChoicesAreDuplicated()
    {
        var request = Valid();
        request.Choice2 = "Dakar";

        Assert.False(IsValid(request, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_IsInvalid_WhenTheQuestionOrAChoiceIsBlank(string blank)
    {
        var noQuestion = Valid();
        noQuestion.Question = blank;
        var noChoice = Valid();
        noChoice.Choice3 = blank;

        Assert.False(IsValid(noQuestion, out _));
        Assert.False(IsValid(noChoice, out _));
    }
}
