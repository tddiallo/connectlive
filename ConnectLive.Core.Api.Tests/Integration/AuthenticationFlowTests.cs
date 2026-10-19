using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConnectLive.Portal.Shared.Request;
using ConnectLive.Portal.Shared.Request.Sessions;

namespace ConnectLive.Core.Api.Tests.Integration;

/// <summary>Parcours HTTP complet : enregistrement → jeton → sessions protégées, et refus pour tout le reste.</summary>
public sealed class AuthenticationFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly List<Guid> _questionIds;

    public AuthenticationFlowTests(ApiFactory factory)
    {
        _factory = factory;
        _questionIds = factory.SeedQuestions(2);
    }

    private HttpClient Anonymous() => _factory.CreateClient();

    private async Task<string> Register(string email)
    {
        var response = await Anonymous().PostAsJsonAsync("/User/save-profile", new SaveUserRequest
        {
            UserFirstName = "Alice", LastName = "Martin", Email = email, Nickname = "ali"
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("signature").GetProperty("secret").GetString()!;
    }

    private HttpClient As(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateSession(HttpClient host, Guid? bodyHostId = null)
    {
        var response = await host.PostAsJsonAsync("/Session/create", new CreateSessionRequest
        {
            Title = "Quiz",
            HostUserId = bodyHostId ?? Guid.Empty,
            QuestionIds = _questionIds
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    [Theory]
    [InlineData("GET", "/Session/by-code/ABCDEF")]
    [InlineData("POST", "/Session/create")]
    [InlineData("GET", "/User/get-profiles")]
    [InlineData("GET", "/Question/all")]
    public async Task ProtectedRoutes_Return401_WithoutToken(string method, string url)
    {
        var response = await Anonymous().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoutes_Return401_WithAForgedToken()
    {
        var response = await As("not.a.jwt").GetAsync("/User/get-profiles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsAToken_ThatOpensProtectedRoutes()
    {
        var token = await Register("flow-1@example.com");

        var response = await As(token).GetAsync("/User/get-profiles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Profiles_NeverExposeSecrets()
    {
        var token = await Register("flow-2@example.com");

        var body = await As(token).GetStringAsync("/User/get-profiles");

        using var doc = JsonDocument.Parse(body);
        Assert.All(doc.RootElement.EnumerateArray(), u =>
            Assert.Equal(JsonValueKind.Null, u.GetProperty("signature").ValueKind));
    }

    [Fact]
    public async Task Register_Returns409_ForADuplicateEmail_And400_ForAnInvalidOne()
    {
        await Register("flow-3@example.com");

        var duplicate = await Anonymous().PostAsJsonAsync("/User/save-profile", new SaveUserRequest
        { UserFirstName = "B", LastName = "B", Email = "FLOW-3@example.com", Nickname = "b" });
        var invalid = await Anonymous().PostAsJsonAsync("/User/save-profile", new SaveUserRequest
        { UserFirstName = "B", LastName = "B", Email = "not-an-email", Nickname = "b" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task OnlyTheHost_CanControlTheSession()
    {
        var host = As(await Register("host@example.com"));
        var stranger = As(await Register("stranger@example.com"));
        var sessionId = await CreateSession(host);

        var byStranger = await stranger.PostAsync($"/Session/{sessionId}/start", null);
        var byHost = await host.PostAsync($"/Session/{sessionId}/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, byStranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byHost.StatusCode);
    }

    [Fact]
    public async Task HostUserIdInTheBody_IsIgnored_TheCallerBecomesTheHost()
    {
        var client = As(await Register("caller@example.com"));

        // Si l'hôte était celui du corps de requête, l'appelant recevrait 403 en démarrant sa propre session.
        var sessionId = await CreateSession(client, bodyHostId: Guid.NewGuid());
        var start = await client.PostAsync($"/Session/{sessionId}/start", null);

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
    }
}
