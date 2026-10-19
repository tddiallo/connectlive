namespace ConnectLive.Domain.Exceptions;

/// <summary>
/// L'appelant est identifié mais n'a pas le droit d'effectuer l'opération (ex. piloter la session d'un autre animateur).
/// Traduite en HTTP 403 par le middleware d'exceptions. À distinguer de <see cref="UnauthorizedAccessException"/> (401, non identifié).
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
