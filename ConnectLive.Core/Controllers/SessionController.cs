using ConnectLive.Core.Api.Auth;
using ConnectLive.Portal.Shared;
using ConnectLive.Portal.Shared.Realtime;
using ConnectLive.Portal.Shared.Request.Sessions;
using ConnectLive.Portal.Shared.Response.Sessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static ConnectLive.Core.Api.Commands.SessionCommands;
using static ConnectLive.Core.Api.Queries.SessionQueries;

namespace ConnectLive.Core.Controllers;

/// <summary>
/// Gestion des sessions de quiz (voir docs/SESSIONS.md). Toutes les routes exigent un jeton Bearer
/// (voir docs/AUTHENTIFICATION.md). Les erreurs métier sont levées par les handlers et traduites en HTTP par
/// <c>ExceptionHandlerMiddleware</c> : 400 (validation), 403 (pas l'animateur), 404 (inconnue), 409 (état invalide).
/// </summary>
[ApiController]
[Authorize]
[Route("[controller]")]
public class SessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Crée une session en <c>Lobby</c> ; l'appelant en devient l'animateur. Renvoie son code de participation.</summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(Result<SessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreateSessionRequest request, CancellationToken cancellationToken) =>
        await Respond(new CreateSessionCommand(request, User.GetUserId()), "Session created.", cancellationToken);

    /// <summary>État d'une session (utile aussi pour resynchroniser un client).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        await Respond(new GetSessionQuery(id), null, cancellationToken);

    /// <summary>Résout le code saisi par un joueur (insensible à la casse).</summary>
    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken) =>
        await Respond(new GetSessionByCodeQuery(code), null, cancellationToken);

    /// <summary><c>Lobby → Running</c> ; diffuse la première question. Réservé à l'animateur.</summary>
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        await Respond(new StartSessionCommand(id, User.GetUserId()), "Session started.", cancellationToken);

    /// <summary>Question suivante ; termine la session après la dernière. Réservé à l'animateur.</summary>
    [HttpPost("{id:guid}/next")]
    public async Task<IActionResult> Next(Guid id, CancellationToken cancellationToken) =>
        await Respond(new NextQuestionCommand(id, User.GetUserId()), null, cancellationToken);

    /// <summary>Termine la session de façon anticipée. Réservé à l'animateur.</summary>
    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, CancellationToken cancellationToken) =>
        await Respond(new EndSessionCommand(id, User.GetUserId()), "Session ended.", cancellationToken);

    /// <summary>
    /// Répond à la question courante. La correction est faite côté serveur ; 409 si la session n'est pas en cours,
    /// si la question n'est plus la courante ou si le joueur a déjà répondu ; 403 pour l'animateur.
    /// </summary>
    [HttpPost("{id:guid}/answer")]
    [ProducesResponseType(typeof(Result<AnswerResultResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Answer(Guid id, SubmitAnswerRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SubmitAnswerCommand(id, User.GetUserId(), request), cancellationToken);
        return Ok(Result<AnswerResultResponse>.Success(result));
    }

    /// <summary>Classement courant de la session (aussi poussé en temps réel via <c>LeaderboardUpdated</c>).</summary>
    [HttpGet("{id:guid}/leaderboard")]
    [ProducesResponseType(typeof(Result<IReadOnlyList<LeaderboardEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Leaderboard(Guid id, CancellationToken cancellationToken)
    {
        var board = await _mediator.Send(new GetLeaderboardQuery(id), cancellationToken);
        return Ok(Result<IReadOnlyList<LeaderboardEntryDto>>.Success(board));
    }

    private async Task<IActionResult> Respond(IRequest<SessionResponse> request, string? message, CancellationToken cancellationToken)
    {
        var session = await _mediator.Send(request, cancellationToken);
        return Ok(message is null
            ? Result<SessionResponse>.Success(session)
            : Result<SessionResponse>.Success(session, message));
    }
}
