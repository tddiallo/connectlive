using System.Security.Claims;
using System.Text;
using ConnectLive.Portal.Shared.Response;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ConnectLive.Core.Api.Auth;

/// <summary>Émet les jetons d'accès de l'API.</summary>
public interface ITokenService
{
    /// <summary>Émet un jeton signé identifiant l'utilisateur (<c>sub</c> = son identifiant).</summary>
    Token Issue(Guid userId, string nickname);
}

/// <summary>JWT HMAC-SHA256 : <c>sub</c> (identifiant), <c>nickname</c>, <c>jti</c>, <c>iat/nbf/exp</c>, <c>iss</c>, <c>aud</c>.</summary>
public sealed class TokenService : ITokenService
{
    public const string NicknameClaim = "nickname";

    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
    }

    public Token Issue(Guid userId, string nickname)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_options.ExpiryMinutes),
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(NicknameClaim, nickname),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            })
        };

        return new Token { Time = now, Secret = _handler.CreateToken(descriptor) };
    }
}
