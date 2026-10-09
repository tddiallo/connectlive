namespace ConnectLive.Portal.Shared.Request.Sessions;

/// <summary>Demande de création d'une session de quiz.</summary>
public class CreateSessionRequest
{
    /// <summary>Titre affiché aux joueurs (1 à 100 caractères).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Utilisateur qui anime la session.</summary>
    public Guid HostUserId { get; set; }

    /// <summary>Questions à poser, dans l'ordre de passage (au moins une, sans doublon).</summary>
    public List<Guid> QuestionIds { get; set; } = new();
}
