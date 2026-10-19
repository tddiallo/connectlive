using System.Security.Claims;
using System.Text;
using ConnectLive.Portal.Shared.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ConnectLive.Core.Api.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// Branche l'authentification JWT Bearer et le service d'émission de jetons. Les options sont validées au
    /// démarrage (<see cref="JwtOptions"/>). Pour SignalR, le jeton est lu dans <c>?access_token=</c> (les WebSockets
    /// ne peuvent pas envoyer d'en-tête <c>Authorization</c>) <b>uniquement</b> sur le chemin du hub.
    /// </summary>
    public static IServiceCollection AddConnectLiveAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITokenService, TokenService>();

        // Même source de configuration que les options liées ci-dessus : la validation au démarrage garantit
        // qu'on n'arrive jamais ici avec une clé invalide.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(SessionHubEvents.HubPath))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();
        return services;
    }

    /// <summary>Identifiant de l'utilisateur authentifié (claim <c>sub</c>).</summary>
    /// <exception cref="UnauthorizedAccessException">Aucun identifiant valide dans le jeton (HTTP 401).</exception>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Authenticated user id is missing or invalid.");
}
