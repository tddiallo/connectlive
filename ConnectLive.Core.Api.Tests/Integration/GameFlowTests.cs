using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConnectLive.Portal.Shared.Request;
using ConnectLive.Portal.Shared.Request.Questions;
using ConnectLive.Portal.Shared.Request.Sessions;

namespace ConnectLive.Core.Api.Tests.Integration;

/// <summary>Une partie complète via HTTP : l'animateur crée ses questions et sa session, un joueur répond, le classement se met à jour.</summary>
public sealed class GameFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public GameFlowTests(ApiFactory factory)
    {
        _factory = factory;
        factory.SeedQuestions(0); // crée le schéma
    }

    private async Task<(HttpClient Client, Guid Id)> RegisterAs(string email, string nickname)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/User/save-profile", new SaveUserRequest
        { UserFirstName = nickname, LastName = "Test", Email = email, Nickname = nickname });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            doc.RootElement.GetProperty("signature").GetProperty("secret").GetString());
        return (client, doc.RootElement.GetProperty("id").GetGuid());
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static CreateQuestionRequest Question(string text) => new()
    {
        Question = text, Choice1 = "A", Choice2 = "B", Choice3 = "C", Choice4 = "D", CorrectAnswer = "B"
    };

    [Fact]
    public async Task FullGame_FromQuestionCreationToLeaderboard()
    {
        var (host, _) = await RegisterAs("game-host@example.com", "host");
        var (player, _) = await RegisterAs("game-player@example.com", "player");

        var q1 = (await Data(await host.PostAsJsonAsync("/Question/save", Question("Q1 ?")))).GetProperty("id").GetGuid();
        var q2 = (await Data(await host.PostAsJsonAsync("/Question/save", Question("Q2 ?")))).GetProperty("id").GetGuid();
        var session = await Data(await host.PostAsJsonAsync("/Session/create", new CreateSessionRequest { Title = "Quiz", QuestionIds = new() { q1, q2 } }));
        var sessionId = session.GetProperty("id").GetGuid();

        (await host.PostAsync($"/Session/{sessionId}/start", null)).EnsureSuccessStatusCode();
        var correct = await Data(await player.PostAsJsonAsync($"/Session/{sessionId}/answer", new SubmitAnswerRequest { QuestionId = q1, Answer = "B" }));
        var again = await player.PostAsJsonAsync($"/Session/{sessionId}/answer", new SubmitAnswerRequest { QuestionId = q1, Answer = "B" });
        var hostAnswer = await host.PostAsJsonAsync($"/Session/{sessionId}/answer", new SubmitAnswerRequest { QuestionId = q1, Answer = "B" });
        var board = await Data(await player.GetAsync($"/Session/{sessionId}/leaderboard"));

        Assert.True(correct.GetProperty("isCorrect").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, hostAnswer.StatusCode);
        Assert.Equal("player", board[0].GetProperty("nickname").GetString());
        Assert.Equal(1, board[0].GetProperty("rank").GetInt32());
        Assert.Equal(100, board[0].GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task Questions_AreVisibleOnlyToTheirAuthor()
    {
        var (author, _) = await RegisterAs("author@example.com", "author");
        var (other, _) = await RegisterAs("other@example.com", "other");
        await Data(await author.PostAsJsonAsync("/Question/save", Question("Private ?")));

        var mine = await Data(await author.GetAsync("/Question/all"));
        var theirs = await Data(await other.GetAsync("/Question/all"));

        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(0, theirs.GetArrayLength());
    }

    [Fact]
    public async Task SavingAnInvalidQuestion_Returns400()
    {
        var (author, _) = await RegisterAs("invalid-q@example.com", "iq");
        var bad = Question("Q ?");
        bad.CorrectAnswer = "not a choice";

        var response = await author.PostAsJsonAsync("/Question/save", bad);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
