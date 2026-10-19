using System.ComponentModel.DataAnnotations;

namespace ConnectLive.Core.Api.Auth;

/// <summary>
/// Paramètres des jetons d'accès (section <c>Jwt</c>). La clé de signature n'est <b>jamais</b> dans
/// <c>appsettings.json</c> : variable d'environnement <c>Jwt__SigningKey</c>, user-secrets ou coffre de secrets.
/// Validées au démarrage : une API sans clé valide refuse de démarrer plutôt que d'émettre des jetons falsifiables.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Longueur minimale de la clé HMAC-SHA256 (32 octets = 256 bits).</summary>
    public const int MinSigningKeyLength = 32;

    [Required]
    public string Issuer { get; set; } = "connectlive";

    [Required]
    public string Audience { get; set; } = "connectlive-clients";

    [Required, MinLength(MinSigningKeyLength)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Durée de vie d'un jeton, en minutes.</summary>
    [Range(1, 24 * 60)]
    public int ExpiryMinutes { get; set; } = 8 * 60;
}
