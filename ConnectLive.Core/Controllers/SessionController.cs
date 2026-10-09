using ConnectLive.Portal.Shared;
using ConnectLive.Portal.Shared.Request.Sessions;
using ConnectLive.Portal.Shared.Response.Sessions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using static ConnectLive.Core.Api.Commands.SessionCommands;
using static ConnectLive.Core.Api.Queries.SessionQueries;

namespace ConnectLive.Core.Controllers;

/// <summary>
/// Gestion des sessions de quiz (voir docs/SESSIONS.md). Les erreurs métier sont levées par les handlers et
/// traduites en HTTP par <c>ExceptionHandlerMiddleware</c> : 400 (validation), 404 (inconnue), 409 (état invalide).
/// </summary>
[ApiController]
[Route("[controller]")]
public class SessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Crée une session en <c>Lobby</c> et renvoie son code de participation.</summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(Result<SessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(CreateSessionRequest request, CancellationToken cancellationToken) =>
        await Respond(new CreateSessionCommand(request), "Session created.", cancellationToken);

    /// <summary>État d'une session (utile aussi pour resynchroniser un client).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        await Respond(new GetSessionQuery(id), null, cancellationToken);

    /// <summary>Résout le code saisi par un joueur (insensible à la casse).</summary>
    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken) =>
        await Respond(new GetSessionByCodeQuery(code), null, cancellationToken);

    /// <summary><c>Lobby → Running</c> ; diffuse la première question.</summary>
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        await Respond(new StartSessionCommand(id), "Session started.", cancellationToken);

    /// <summary>Question suivante ; termine la session après la dernière.</summary>
    [HttpPost("{id:guid}/next")]
    public async Task<IActionResult> Next(Guid id, CancellationToken cancellationToken) =>
        await Respond(new NextQuestionCommand(id), null, cancellationToken);

    /// <summary>Termine la session de façon anticipée.</summary>
    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, CancellationToken cancellationToken) =>
        await Respond(new EndSessionCommand(id), "Session ended.", cancellationToken);

    private async Task<IActionResult> Respond(IRequest<SessionResponse> request, string? message, CancellationToken cancellationToken)
    {
        var session = await _mediator.Send(request, cancellationToken);
        return Ok(message is null
            ? Result<SessionResponse>.Success(session)
            : Result<SessionResponse>.Success(session, message));
    }
}
