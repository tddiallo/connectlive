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
/// Chaque commande porte l'identifiant de l'<b>utilisateur authentifié</b> qui la lance : seul l'animateur de la
/// session peut la piloter (voir docs/AUTHENTIFICATION.md).
/// </summary>
public static class SessionCommands
{
    #region CreateSession
    public class CreateSessionCommand : IntegrationEvent, IRequest<SessionResponse>
    {
        /// <param name="session">Contenu de la session.</param>
        /// <param name="hostUserId">Utilisateur authentifié : il devient l'animateur (le corps de la requête ne le décide pas).</param>
        public CreateSessionCommand(CreateSessionRequest session, Guid hostUserId)
        {
            Session = session;
            HostUserId = hostUserId;
        }

        public CreateSessionRequest Session { get; set; }

        public Guid HostUserId { get; set; }
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
            var session = SessionEntity.Create(request.Title, command.HostUserId, questionIds, code, _timeProvider.GetUtcNow().UtcDateTime);

            await _unitOfWork.Sessions.AddAsync(session, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

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
        public StartSessionCommand(Guid sessionId, Guid requesterId)
        {
            SessionId = sessionId;
            RequesterId = requesterId;
        }

        public Guid SessionId { get; set; }

        /// <summary>Utilisateur authentifié qui lance la commande ; doit être l'animateur.</summary>
        public Guid RequesterId { get; set; }
    }

    public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, SessionResponse>
    {
        private readonly SessionTransition _transition;

        public StartSessionCommandHandler(IServiceProvider serviceProvider)
        {
            _transition = new SessionTransition(serviceProvider);
        }

        public Task<SessionResponse> Handle(StartSessionCommand command, CancellationToken cancellationToken) =>
            _transition.ExecuteAsync(command.SessionId, command.RequesterId, (session, now) => { session.Start(now); return true; }, cancellationToken);
    }
    #endregion

    #region NextQuestion
    public class NextQuestionCommand : IntegrationEvent, IRequest<SessionResponse>
    {
        public NextQuestionCommand(Guid sessionId, Guid requesterId)
        {
            SessionId = sessionId;
            RequesterId = requesterId;
        }

        public Guid SessionId { get; set; }

        /// <summary>Utilisateur authentifié qui lance la commande ; doit être l'animateur.</summary>
        public Guid RequesterId { get; set; }
    }

    public class NextQuestionCommandHandler : IRequestHandler<NextQuestionCommand, SessionResponse>
    {
        private readonly SessionTransition _transition;

        public NextQuestionCommandHandler(IServiceProvider serviceProvider)
        {
            _transition = new SessionTransition(serviceProvider);
        }

        public Task<SessionResponse> Handle(NextQuestionCommand command, CancellationToken cancellationToken) =>
            _transition.ExecuteAsync(command.SessionId, command.RequesterId, (session, now) => session.MoveNext(now), cancellationToken);
    }
    #endregion

    #region EndSession
    public class EndSessionCommand : IntegrationEvent, IRequest<SessionResponse>
    {
        public EndSessionCommand(Guid sessionId, Guid requesterId)
        {
            SessionId = sessionId;
            RequesterId = requesterId;
        }

        public Guid SessionId { get; set; }

        /// <summary>Utilisateur authentifié qui lance la commande ; doit être l'animateur.</summary>
        public Guid RequesterId { get; set; }
    }

    public class EndSessionCommandHandler : IRequestHandler<EndSessionCommand, SessionResponse>
    {
        private readonly SessionTransition _transition;

        public EndSessionCommandHandler(IServiceProvider serviceProvider)
        {
            _transition = new SessionTransition(serviceProvider);
        }

        public Task<SessionResponse> Handle(EndSessionCommand command, CancellationToken cancellationToken) =>
            _transition.ExecuteAsync(command.SessionId, command.RequesterId, (session, now) => { session.End(now); return false; }, cancellationToken);
    }
    #endregion

    #region SubmitAnswer
    public class SubmitAnswerCommand : IntegrationEvent, IRequest<AnswerResultResponse>
    {
        public SubmitAnswerCommand(Guid sessionId, Guid playerId, SubmitAnswerRequest answer)
        {
            SessionId = sessionId;
            PlayerId = playerId;
            Answer = answer;
        }

        public Guid SessionId { get; set; }

        /// <summary>Joueur authentifié qui répond.</summary>
        public Guid PlayerId { get; set; }

        public SubmitAnswerRequest Answer { get; set; }
    }

    /// <summary>
    /// Corrige la réponse <b>côté serveur</b> (la bonne réponse ne quitte jamais l'API avant), l'enregistre, puis diffuse
    /// le classement. Règles : session en cours, question courante, choix proposé, une seule réponse par joueur et
    /// par question, l'animateur ne joue pas.
    /// </summary>
    public class SubmitAnswerCommandHandler : IRequestHandler<SubmitAnswerCommand, AnswerResultResponse>
    {
        /// <summary>Points d'une bonne réponse (barème fixe pour l'instant, pas de bonus de rapidité).</summary>
        public const int PointsPerCorrectAnswer = 100;

        private const string AlreadyAnsweredMessage = "You have already answered this question.";

        private readonly ApplicationDbContext _dbContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISessionNotifier _notifier;
        private readonly TimeProvider _timeProvider;

        public SubmitAnswerCommandHandler(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            _notifier = serviceProvider.GetRequiredService<ISessionNotifier>();
            _timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        }

        public async Task<AnswerResultResponse> Handle(SubmitAnswerCommand command, CancellationToken cancellationToken)
        {
            var request = command.Answer;
            var session = await _dbContext.GetSessionOrThrowAsync(command.SessionId, cancellationToken);

            if (session.HostUserId == command.PlayerId)
                throw new ForbiddenException("The host cannot answer questions of their own session.");
            if (session.Status != SessionStatus.Running)
                throw new ConflictException($"Session is not running (status '{session.Status}').");
            if (session.CurrentQuestionId != request.QuestionId)
                throw new ConflictException("This question is not the current one.");

            var question = await _dbContext.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken)
                ?? throw new KeyNotFoundException($"Question '{request.QuestionId}' cannot be found.");
            if (!question.Choices.Contains(request.Answer))
                throw new ArgumentException("Answer must be one of the proposed choices.", nameof(request.Answer));

            if (await _dbContext.SessionAnswers.AnyAsync(a =>
                    a.SessionId == session.Id && a.QuestionId == question.Id && a.UserId == command.PlayerId, cancellationToken))
                throw new ConflictException(AlreadyAnsweredMessage);

            var isCorrect = string.Equals(question.CorrectAnswer, request.Answer, StringComparison.Ordinal);
            var points = isCorrect ? PointsPerCorrectAnswer : 0;

            await _unitOfWork.SessionAnswers.AddAsync(new SessionAnswerEntity
            {
                SessionId = session.Id,
                QuestionId = question.Id,
                UserId = command.PlayerId,
                Answer = request.Answer,
                IsCorrect = isCorrect,
                Points = points,
                AnsweredAt = _timeProvider.GetUtcNow().UtcDateTime
            }, cancellationToken);
            try
            {
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Deux requêtes simultanées du même joueur : la seconde est arrêtée par l'index unique.
                throw new ConflictException(AlreadyAnsweredMessage);
            }

            // Notification après la persistance, comme pour les transitions : la base fait foi.
            var leaderboard = await _dbContext.LoadAsync(session.Id, cancellationToken);
            await _notifier.NotifyLeaderboardUpdatedAsync(session.Id.ToString(), leaderboard, cancellationToken);

            return new AnswerResultResponse
            {
                QuestionId = question.Id,
                IsCorrect = isCorrect,
                Points = points,
                TotalScore = leaderboard.First(e => e.UserId == command.PlayerId).Score
            };
        }
    }
    #endregion
}
