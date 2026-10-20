using System.Net;
using ConnectLive.Core.Api.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using StackExchange.Redis;

namespace ConnectLive.Core.Api.Tests.Integration;

public sealed class HealthCheckTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthCheckTests(ApiFactory factory)
    {
        _factory = factory;
        factory.SeedQuestions(0); // crée le schéma
    }

    [Fact]
    public async Task Live_Returns200_WithoutAuthentication()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_Returns200_WhenTheDatabaseIsReachable_AndExposesNoDetails()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RedisCheck_IsUnhealthy_WhenRedisDoesNotAnswer()
    {
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await new RedisHealthCheck(redis).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
