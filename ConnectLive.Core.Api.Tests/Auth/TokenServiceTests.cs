using System.ComponentModel.DataAnnotations;
using System.Text;
using ConnectLive.Core.Api.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ConnectLive.Core.Api.Tests.Auth;

public class TokenServiceTests
{
    private const string Key = "unit-test-signing-key-0123456789-abcdef";

    private static JwtOptions Options(string key = Key) => new() { SigningKey = key, ExpiryMinutes = 60 };

    private static TokenService Service(JwtOptions options, TimeProvider? time = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options), time ?? TimeProvider.System);

    private static async Task<TokenValidationResult> Validate(string jwt, string key = Key) =>
        await new JsonWebTokenHandler().ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            ValidIssuer = "connectlive",
            ValidAudience = "connectlive-clients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        });

    [Fact]
    public async Task Issue_ProducesAVerifiableToken_CarryingTheUserId()
    {
        var userId = Guid.NewGuid();

        var token = Service(Options()).Issue(userId, "ali");
        var result = await Validate(token.Secret);

        Assert.True(result.IsValid);
        Assert.Equal(userId.ToString(), ((JsonWebToken)result.SecurityToken).Subject);
        Assert.Equal("ali", result.Claims[TokenService.NicknameClaim]);
    }

    [Fact]
    public async Task Issue_TokenSignedWithAnotherKey_IsRejected()
    {
        var token = Service(Options("another-signing-key-0123456789-abcdefgh")).Issue(Guid.NewGuid(), "ali");

        Assert.False((await Validate(token.Secret)).IsValid);
    }

    [Fact]
    public async Task Issue_TokenIsRejected_AfterExpiry()
    {
        var token = Service(Options(), new FixedTime(DateTimeOffset.UtcNow.AddHours(-2))).Issue(Guid.NewGuid(), "ali");

        Assert.False((await Validate(token.Secret)).IsValid);
    }

    [Fact]
    public void Issue_TwoTokensForTheSameUser_AreDifferent()
    {
        var service = Service(Options());
        var id = Guid.NewGuid();

        Assert.NotEqual(service.Issue(id, "ali").Secret, service.Issue(id, "ali").Secret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    public void Options_AreInvalid_WhenSigningKeyIsMissingOrShort(string key)
    {
        var options = Options(key);

        Assert.False(Validator.TryValidateObject(options, new ValidationContext(options), new List<ValidationResult>(), true));
    }

    [Fact]
    public void Options_AreValid_WithAStrongKey()
    {
        var options = Options();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), new List<ValidationResult>(), true));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
