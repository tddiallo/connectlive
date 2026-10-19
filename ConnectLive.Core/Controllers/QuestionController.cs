using ConnectLive.Core.Api.Auth;
using ConnectLive.Portal.Shared;
using ConnectLive.Portal.Shared.Request.Questions;
using ConnectLive.Portal.Shared.Response.Questions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static ConnectLive.Core.Api.Commands.QuestionCommands;
using static ConnectLive.Core.Api.Queries.QuestionQueries;

namespace ConnectLive.Core.Controllers;

/// <summary>
/// Questions de l'animateur (voir docs/QUESTIONS-REPONSES.md). Chaque utilisateur ne voit que les siennes.
/// </summary>
[ApiController]
[Authorize]
[Route("[controller]")]
public class QuestionController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuestionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Questions créées par l'utilisateur authentifié (avec leur bonne réponse).</summary>
    [HttpGet]
    [Route("all")]
    public async Task<IActionResult> GetQuestions(CancellationToken cancellationToken)
    {
        var questions = await _mediator.Send(new GetMyQuestionsQuery(User.GetUserId()), cancellationToken);
        return Ok(Result<List<CreateQuestionResponse>>.Success(questions));
    }

    /// <summary>Crée une question ; l'appelant en est l'auteur. 400 si la demande est invalide.</summary>
    [HttpPost]
    [Route("save")]
    public async Task<IActionResult> SaveQuestion(CreateQuestionRequest request, CancellationToken cancellationToken)
    {
        var created = await _mediator.Send(new CreateQuestionCommand(request, User.GetUserId()), cancellationToken);
        return Ok(Result<CreateQuestionResponse>.Success(created, "Question saved successfully!"));
    }
}
