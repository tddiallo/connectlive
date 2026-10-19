using System.ComponentModel.DataAnnotations;

namespace ConnectLive.Portal.Shared.Request;

/// <summary>Demande d'enregistrement d'un utilisateur. Tous les champs sont obligatoires.</summary>
public class SaveUserRequest
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 254;

    [Required, StringLength(MaxNameLength, MinimumLength = 1)]
    public string UserFirstName { get; set; } = string.Empty;

    [Required, StringLength(MaxNameLength, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(MaxEmailLength)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(MaxNameLength, MinimumLength = 1)]
    public string Nickname { get; set; } = string.Empty;
}
