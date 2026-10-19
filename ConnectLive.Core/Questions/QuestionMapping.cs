using ConnectLive.Portal.Shared.Response.Questions;
using ConntectLive.DAL;

namespace ConnectLive.Core.Api.Questions;

public static class QuestionMapping
{
    /// <summary>Vue « auteur » d'une question (avec la bonne réponse).</summary>
    public static CreateQuestionResponse ToResponse(this QuestionEntity question)
    {
        var choices = question.Choices ?? new List<string>();
        return new CreateQuestionResponse
        {
            Id = question.Id,
            Question = question.Question,
            Category = question.Category,
            CorrectAnswer = question.CorrectAnswer,
            Choice1 = choices.ElementAtOrDefault(0) ?? string.Empty,
            Choice2 = choices.ElementAtOrDefault(1) ?? string.Empty,
            Choice3 = choices.ElementAtOrDefault(2) ?? string.Empty,
            Choice4 = choices.ElementAtOrDefault(3) ?? string.Empty
        };
    }
}
