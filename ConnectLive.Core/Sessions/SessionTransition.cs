using ConnectLive.Core.Api.Realtime;
using ConnectLive.Domain.Exceptions;
using ConnectLive.Portal.Shared.Response.Sessions;
using ConntectLive.DAL;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Sessions;

/// <summary>
/// Enchaîne les étapes communes à Start / Next / End : charger la session, appliquer la transition de domaine,
/// persister, puis notifier les joueurs. Évite de dupliquer ce squelette dans chaque handler.
/// </summary>
public sealed class SessionTransition
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public SessionTransition(IServiceProvider serviceProvider)
    {
        _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        _notifier = serviceProvider.GetRequiredService<ISessionNotifier>();
        _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
    }

    /// <param name="sessionId">Session à modifier.</param>
    /// <param name="requesterId">Utilisateur authentifié ; doit être l'animateur de la session.</param>
    /// <param name="transition">Applique la règle de domaine ; renvoie <c>true</c> si une question est (toujours) courante.</param>
    /// <exception cref="KeyNotFoundException">Session inconnue (404).</exception>
    /// <exception cref="ForbiddenException">L'appelant n'est pas l'animateur de la session (403).</exception>
    /// <exception cref="ConflictException">Transition interdite ou modification concurrente (409).</exception>
    public async Task<SessionResponse> ExecuteAsync(Guid sessionId, Guid requesterId, Func<SessionEntity, DateTime, bool> transition, CancellationToken cancellationToken)
    {
        var session = await _dbContext.GetSessionOrThrowAsync(sessionId, cancellationToken);
        if (session.HostUserId != requesterId)
            throw new ForbiddenException("Only the session host can control this session.");

        var hasCurrentQuestion = transition(session, _timeProvider.GetUtcNow().UtcDateTime);
        await CommitAsync(cancellationToken);

        // Notification après la persistance : l'état en base fait foi, un client peut toujours se resynchroniser.
        var channel = session.Id.ToString();
        if (hasCurrentQuestion)
            await _notifier.NotifyQuestionChangedAsync(channel, await _dbContext.ToCurrentQuestionDtoAsync(session, cancellationToken), cancellationToken);
        else
            await _notifier.NotifySessionEndedAsync(channel, cancellationToken);

        return session.ToResponse();
    }

    private async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The session was modified concurrently, please retry.");
        }
    }
}
