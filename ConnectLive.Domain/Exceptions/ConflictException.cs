namespace ConnectLive.Domain.Exceptions;

/// <summary>
/// Opération refusée car incompatible avec l'état courant d'une ressource (ex. démarrer une session déjà terminée).
/// Traduite en HTTP 409 par le middleware d'exceptions.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
