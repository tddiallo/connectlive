using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConnectLive.Core.Api.Tests.Realtime;
using ConnectLive.Portal.Shared.Realtime;
using ConnectLive.Portal.Shared.Request;
using ConnectLive.Portal.Shared.Request.Questions;
using ConnectLive.Portal.Shared.Request.Sessions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ConnectLive.Core.Api.Tests.Integration;

/// <summary>
/// Deux instances d'API (deux serveurs de test) reliées par le même Redis, avec un client SignalR sur chacune.
/// Prouve que le backplane relaie les messages entre instances et que la présence est partagée
/// (voir docs/SIGNALR-BACKPLANE-REDIS.md). Exécuté seulement si <c>CONNECTLIVE_TEST_REDIS</c> est défini.
/// </summary>
public sealed class SignalRBackplaneTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static async Task<(HttpClient Http, string Token, Guid Id)> RegisterOn(ApiFactory instance, string nickname)
    {
        var response = await instance.CreateClient().PostAsJsonAsync("/User/save-profile", new SaveUserRequest
        { UserFirstName = nickname, LastName = "T", Email = $"{nickname}-{Guid.NewGuid():N}@example.com", Nickname = nickname });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("signature").GetProperty("secret").GetString()!;
        var http = instance.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (http, token, doc.RootElement.GetProperty("id").GetGuid());
    }

    private static HubConnection Connect(ApiFactory instance, string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(instance.Server.BaseAddress, SessionHubEvents.HubPath), options =>
            {
                options.HttpMessageHandlerFactory = _ => instance.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling; // le TestServer n'ouvre pas de vrai socket
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private static async Task<T> Data<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [RedisFact]
    public async Task AMessageSentByInstanceA_ReachesAClientConnectedToInstanceB()
    {
        var redis = RedisPresenceTrackerTests.ConnectionString!;
        var database = $"backplane{Guid.NewGuid():N}";
        using var instanceA = ApiFactory.Shared(database, redis);
        using var instanceB = ApiFactory.Shared(database, redis);
        instanceA.SeedQuestions(0); // crée le schéma partagé

        // Animateur sur A ; Alice connectée à A ; Bob connecté à B.
        var (host, _, _) = await RegisterOn(instanceA, "host");
        var (_, aliceToken, aliceId) = await RegisterOn(instanceA, "alice");
        var (bobHttp, bobToken, bobId) = await RegisterOn(instanceB, "bob");

        var q1 = (await Data<JsonElement>(await host.PostAsJsonAsync("/Question/save", new CreateQuestionRequest
        { Question = "Q1 ?", Choice1 = "A", Choice2 = "B", Choice3 = "C", Choice4 = "D", CorrectAnswer = "B" }))).GetProperty("id").GetGuid();
        var sessionId = (await Data<JsonElement>(await host.PostAsJsonAsync("/Session/create",
            new CreateSessionRequest { Title = "Quiz", QuestionIds = new() { q1 } }))).GetProperty("id").GetGuid();

        await using var alice = Connect(instanceA, aliceToken);
        await using var bob = Connect(instanceB, bobToken);
        var bobReceivesQuestion = new TaskCompletionSource<SessionQuestionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var aliceReceivesLeaderboard = new TaskCompletionSource<LeaderboardEntryDto[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        bob.On<SessionQuestionDto>(SessionHubEvents.QuestionChanged, q => bobReceivesQuestion.TrySetResult(q));
        alice.On<LeaderboardEntryDto[]>(SessionHubEvents.LeaderboardUpdated, l => aliceReceivesLeaderboard.TrySetResult(l));
        await alice.StartAsync();
        await bob.StartAsync();

        var seenByAlice = await alice.InvokeAsync<string[]>("JoinSession", sessionId.ToString());
        var seenByBob = await bob.InvokeAsync<string[]>("JoinSession", sessionId.ToString());

        // Présence partagée : Bob (instance B) voit Alice (instance A) dans la liste.
        Assert.Contains(aliceId.ToString(), seenByBob);
        Assert.Contains(bobId.ToString(), seenByBob);
        Assert.Contains(aliceId.ToString(), seenByAlice);

        // Backplane A → B : l'animateur démarre sur l'instance A, le client de l'instance B reçoit la question.
        (await host.PostAsync($"/Session/{sessionId}/start", null)).EnsureSuccessStatusCode();
        var question = await bobReceivesQuestion.Task.WaitAsync(Timeout);
        Assert.Equal(q1, question.QuestionId);

        // Backplane B → A : Bob répond via l'instance B, le client d'Alice (instance A) reçoit le classement.
        (await bobHttp.PostAsJsonAsync($"/Session/{sessionId}/answer", new SubmitAnswerRequest { QuestionId = q1, Answer = "B" })).EnsureSuccessStatusCode();
        var leaderboard = await aliceReceivesLeaderboard.Task.WaitAsync(Timeout);
        Assert.Equal(bobId, leaderboard.Single().UserId);
        Assert.Equal(100, leaderboard.Single().Score);
    }
}
