namespace ConnectLive.Portal.Shared.Response.Questions;

/// <summary>
/// Question telle que vue par son <b>auteur</b> (qui doit pouvoir relire la bonne réponse). Les joueurs, eux,
/// ne reçoivent jamais <see cref="CorrectAnswer"/> : ils voient <c>SessionQuestionDto</c>.
/// </summary>
public class CreateQuestionResponse
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Choice1 { get; set; } = string.Empty;
    public string Choice2 { get; set; } = string.Empty;
    public string Choice3 { get; set; } = string.Empty;
    public string Choice4 { get; set; } = string.Empty;
}
