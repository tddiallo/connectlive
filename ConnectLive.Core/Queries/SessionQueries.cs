using ConnectLive.Application;
using ConnectLive.Core.Api.Sessions;
using ConnectLive.Portal.Shared.Response.Sessions;
using ConntectLive.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Queries;

/// <summary>
/// Requêtes de lecture des sessions. Volontairement <b>non cachées</b> (<c>ICachable</c>) : l'état d'une session change
/// à chaque question et doit être exact pour la resynchronisation des clients.
/// </summary>
public static class SessionQueries
{
    #region GetSession
    public class GetSessionQuery : IntegrationEvent, IRequest<SessionResponse>
    {
        public GetSessionQuery(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public Guid SessionId { get; set; }
    }

    public class GetSessionQueryHandler : IRequestHandler<GetSessionQuery, SessionResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetSessionQueryHandler(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        }

        public async Task<SessionResponse> Handle(GetSessionQuery request, CancellationToken cancellationToken) =>
            (await _dbContext.GetSessionOrThrowAsync(request.SessionId, cancellationToken)).ToResponse();
    }
    #endregion

    #region GetSessionByCode
    public class GetSessionByCodeQuery : IntegrationEvent, IRequest<SessionResponse>
    {
        public GetSessionByCodeQuery(string code)
        {
            Code = code;
        }

        public string Code { get; set; }
    }

    public class GetSessionByCodeQueryHandler : IRequestHandler<GetSessionByCodeQuery, SessionResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetSessionByCodeQueryHandler(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        }

        public async Task<SessionResponse> Handle(GetSessionByCodeQuery request, CancellationToken cancellationToken)
        {
            // Les codes sont stockés en majuscules (voir SessionCodeGenerator.Alphabet)
            var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
            var session = await _dbContext.Sessions.Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Code == code, cancellationToken)
                ?? throw new KeyNotFoundException($"No session with code '{code}'.");
            return session.ToResponse();
        }
    }
    #endregion
}
