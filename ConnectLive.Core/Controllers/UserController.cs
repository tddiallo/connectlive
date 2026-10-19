using ConnectLive.Core.Api.Queries;
using ConnectLive.Portal.Shared.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using static ConnectLive.Core.Api.Commands.UserCommands;
using static ConnectLive.Core.Api.Queries.UserQueries;

namespace ConnectLive.Core.Controllers;

/// <summary>
/// Utilisateurs. L'enregistrement est public (il émet le jeton d'accès, voir docs/AUTHENTIFICATION.md) mais limité
/// en débit ; la consultation des profils exige un jeton.
/// </summary>
[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    /// <summary>Nom de la politique de limitation de débit appliquée à l'enregistrement.</summary>
    public const string RegistrationRateLimitPolicy = "registration";

    private readonly ILogger<UserController> _logger;
    private readonly IMediator _mediator;


    public UserController(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILogger<UserController>>();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    [Authorize]
    [HttpGet]
    [Route("get-profile/{id}")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var result = await _mediator.Send(new UserQueries.GetUserQuery(id));
        if (result == null) return NotFound($"User with id '{id}' cannot be found!");

        return Ok(result);
    }

    [Authorize]
    [HttpGet]
    [Route("get-profiles")]
    public async Task<IActionResult> GetProfiles()
    {
        var result = await _mediator.Send(new UserQueries.GetUsersQuery());
        return Ok(result);
    }

    /// <summary>
    /// Enregistre un utilisateur et renvoie son jeton d'accès (<c>signature.secret</c>) — la seule fois où il est
    /// délivré. 400 si la demande est invalide, 409 si l'email existe déjà, 429 si trop de demandes.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RegistrationRateLimitPolicy)]
    [HttpPost]
    [Route("save-profile")]
    public async Task<IActionResult> SaveUser(SaveUserRequest request)
    {
        var saveRequest = new SaveUserCommand(request);

        var result = await _mediator.Send(saveRequest);
        return Ok(result);
    }
}
