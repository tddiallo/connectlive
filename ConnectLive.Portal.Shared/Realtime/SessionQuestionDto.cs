namespace ConnectLive.Portal.Shared.Realtime;

/// <summary>
/// Question telle que vue par les joueurs pendant une session. Ne contient volontairement <b>pas</b> la bonne
/// réponse : elle ne doit jamais quitter le serveur avant la correction.
/// </summary>
/// <param name="QuestionId">Identifiant de la question.</param>
/// <param name="Text">Énoncé.</param>
/// <param name="Category">Catégorie (peut être vide).</param>
/// <param name="Choices">Choix proposés.</param>
/// <param name="Index">Position de la question dans la session, à partir de 0.</param>
/// <param name="Total">Nombre total de questions de la session.</param>
public sealed record SessionQuestionDto(
    Guid QuestionId,
    string Text,
    string Category,
    IReadOnlyList<string> Choices,
    int Index,
    int Total);
