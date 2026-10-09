using ConnectLive.Application;
using ConnectLive.Core.Api.Realtime;
using ConnectLive.Core.Api.Sessions;
using ConnectLive.Domain.Exceptions;
using ConnectLive.Portal.Shared.Request.Sessions;
using ConnectLive.Portal.Shared.Response.Sessions;
using ConntectLive.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Commands;

/// <summary>
/// Commandes du cycle de vie d'une session de quiz (voir docs/SESSIONS.md). Les règles de transition sont dans
/// <see cref="SessionEntity"/> ; les handlers orchestrent chargement, persistance et notification temps réel.
/// </summary>
public static class SessionCommands
{
    #region CreateSession
    public class CreateSessionCommand : IntegrationEvent, IRequest<SessionResponse>
    {
        public CreateSessionCommand(CreateSessionRequest session)
        {
            Session = session;
        }

        public CreateSessionRequest Session { get; set; }
    }

    public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, SessionResponse>
    {
        /// <summary>Tirages successifs avant d'abandonner si le code est déjà pris (collision très improbable).</summary>
        private const int MaxCodeAttempts = 5;

        private readonly ApplicationDbContext _dbContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public CreateSessionCommandHandler(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        }

        public async Task<SessionResponse> Handle(CreateSessionCommand command, CancellationToken cancellationToken)
        {
            var request = command.Session;
            var questionIds = request.QuestionIds ?? new List<Guid>();

            var existing = await _dbContext.Questions
                .Where(q => questionIds.Contains(q.Id))
                .Select(q => q.Id)
                .ToListAsync(cancellationToken);
            var missing = questionIds.Except(existing).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Unknown question(s): {string.Join(", ", missing)}.", nameof(request.QuestionIds));

            var code = await GenerateUniqueCodeAsync(cancellationToken);
            var session = SessionEntity.Create(request.Title, request.HostUserId, questionIds, code, _timeProvider.GetUtcNow().UtcDateTime);

            await _unitOfWork.Sessions.AddAsync(session);
            _unitOfWork.Commit();

            return session.ToResponse();
        }

        private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
            {
                var code = SessionCodeGenerator.Generate();
                if (!await _dbContext.Sessions.AnyAsync(s => s.Code == code, cancellationToken))
                    return code;
            }

            throw new ConflictException("Could not allocate a unique session code, please retry.");
        }
    }
    #endregion

    #region StartSession
    public class StartSessionCommand : IntegrationEvent, IRequest<SessionResponse>
    {
        public StartSessionCommand(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public Guid SessionId { get; set; }
    }

    public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, SessionResponse>
    {
        private readonly SessionTransition _transition;

        public StartSessionCommandHandler(IServiceProvider serviceProvider)
        {
            _transition = new SessionTransition(serviceProvider);
        }

        public Task<SessionResponse> Handle(StartSessionCommand command, CancellationToken cancellationToken) =>
            _transition.ExecuteAsync(command.SessionId, (session, now) => { session.Start(now); return true; }, cancellationToken);
    }
    #endregion

    #region NextQuestion
    public class NextQuestionCommand : IntegrationEvent, IRequest<SessionResponse>
    {
        public NextQuestionCommand(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public Guid SessionId { get; set; }
    }

    public class NextQuestionCommandHandler : IRequestHandler<NextQuestionCommand, SessionResponse>
    {
        private readonly SessionTransition _transition;

        public NextQuestionCommandHandler(IServiceProvider serviceProvider)
        {
            _transition = new SessionTransition(serviceProvider);
        }

        public Task<SessionResponse> Handle(NextQuestionCommand command, CancellationToken cancellationToken) =>
            _transition.ExecuteAsync(command.SessionId, (session, now) => session.MoveNext(now), cancellationToken);
    }
    #endregion

    #region EndSession
    public class EndSessionCommand : IntegrationEvent, IRequest<SessionResponse>
    {
        public EndSessionCommand(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public Guid SessionId { get; set; }
    }

    public class EndSessionCommandHandler : IRequestHandler<EndSessionCommand, SessionResponse>
    {
        private readonly SessionTransition _transition;

        public EndSessionCommandHandler(IServiceProvider serviceProvider)
        {
            _transition = new SessionTransition(serviceProvider);
        }

        public Task<SessionResponse> Handle(EndSessionCommand command, CancellationToken cancellationToken) =>
            _transition.ExecuteAsync(command.SessionId, (session, now) => { session.End(now); return false; }, cancellationToken);
    }
    #endregion
}
