using ConnectLive.Core.Api.Extensions;

namespace ConnectLive.Core.Api.Tests;

public class CorsExtensionsTests
{
    [Fact]
    public void ParseOrigins_SplitsTrimsAndDeduplicates()
    {
        var origins = CorsExtensions.ParseOrigins(" http://localhost:3000/ ;http://localhost:5000,; HTTP://localhost:3000 ");

        Assert.Equal(new[] { "http://localhost:3000", "http://localhost:5000" }, origins);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ; ")]
    public void ParseOrigins_ReturnsEmpty_WhenNothingConfigured(string? raw) =>
        Assert.Empty(CorsExtensions.ParseOrigins(raw));
}
