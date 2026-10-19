using System.ComponentModel.DataAnnotations;

namespace ConnectLive.Portal.Shared.Request.Questions;

/// <summary>Création d'une question à choix multiple : 4 choix distincts dont la bonne réponse.</summary>
public class CreateQuestionRequest : IValidatableObject
{
    public const int MaxQuestionLength = 500;
    public const int MaxChoiceLength = 200;
    public const int MaxCategoryLength = 100;

    /// <summary><b>Ignoré</b> : l'identifiant est toujours généré par le serveur. Conservé pour compatibilité.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(MaxQuestionLength)]
    public string Question { get; set; } = string.Empty;

    /// <summary>Catégorie libre (optionnelle).</summary>
    [StringLength(MaxCategoryLength)]
    public string? Category { get; set; }

    /// <summary>Doit être identique (casse comprise) à l'un des quatre choix.</summary>
    [Required, StringLength(MaxChoiceLength)]
    public string CorrectAnswer { get; set; } = string.Empty;

    [Required, StringLength(MaxChoiceLength)]
    public string Choice1 { get; set; } = string.Empty;

    [Required, StringLength(MaxChoiceLength)]
    public string Choice2 { get; set; } = string.Empty;

    [Required, StringLength(MaxChoiceLength)]
    public string Choice3 { get; set; } = string.Empty;

    [Required, StringLength(MaxChoiceLength)]
    public string Choice4 { get; set; } = string.Empty;

    /// <summary>Les quatre choix, dans l'ordre.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> Choices => new[] { Choice1, Choice2, Choice3, Choice4 };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Choices.Distinct(StringComparer.Ordinal).Count() != Choices.Count)
            yield return new ValidationResult("Choices must be distinct.", new[] { nameof(Choice1) });

        if (!Choices.Contains(CorrectAnswer, StringComparer.Ordinal))
            yield return new ValidationResult("CorrectAnswer must be one of the four choices.", new[] { nameof(CorrectAnswer) });
    }
}
