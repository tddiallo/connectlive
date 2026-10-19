using AutoMapper;
using ConnectLive.Application;
using ConnectLive.Application.Interfaces;
using ConnectLive.Portal.Shared.Response;
using ConntectLive.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Queries;

/// <summary>Clés de cache partagées entre les requêtes (lecture) et les commandes (invalidation).</summary>
public static class UserCacheKeys
{
    public const string AllUsers = "AllUsers";
}

public static class UserQueries
{
    #region GetUser
    public class GetUserQuery : IntegrationEvent, IRequest<SaveUserResponse>, ICachable<SaveUserResponse>
    {
        public GetUserQuery(Guid userId)
        {
            UserId = userId;
            Key = userId.ToString();
        }

        public Guid UserId { get; set; }
        public string Key { get; set; }
        public int Expiration { get; set; } = 30;
    }

    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, SaveUserResponse>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMapper _mapper;

        public GetUserQueryHandler(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _mapper = serviceProvider.GetRequiredService<IMapper>();
        }

        public async Task<SaveUserResponse> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            var persisted = await _dbContext.Users.FirstOrDefaultAsync(e => e.Id == request.UserId, cancellationToken);
            return persisted == null ? null : _mapper.Map<SaveUserResponse>(persisted);
        }
    }
    #endregion

    #region GetUsers
    public class GetUsersQuery : IntegrationEvent, IRequest<List<SaveUserResponse>>, ICachable<List<SaveUserResponse>>
    {
        public GetUsersQuery()
        {
        }

        public GetUsersQuery(Guid eventId): base(eventId)
        {
        }

        public string Key { get; set; } = UserCacheKeys.AllUsers;
        public int Expiration { get; set; } = 10;
    }

    public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, List<SaveUserResponse>>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMapper _mapper;

        public GetUsersQueryHandler(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _mapper = serviceProvider.GetRequiredService<IMapper>();
        }

        public async Task<List<SaveUserResponse>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            var result = await _dbContext.Users.AsNoTracking().ToListAsync(cancellationToken);


            return result == null ? null : _mapper.Map<List<SaveUserResponse>>(result);
        }
    }
    #endregion
}
