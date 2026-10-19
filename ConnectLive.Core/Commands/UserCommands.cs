using AutoMapper;
using ConnectLive.Application;
using ConnectLive.Core.Api.Auth;
using ConnectLive.Core.Api.Queries;
using ConnectLive.Domain.Exceptions;
using ConnectLive.Portal.Shared.Request;
using ConnectLive.Portal.Shared.Response;
using ConntectLive.DAL;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ConnectLive.Domain.Contracts;

namespace ConnectLive.Core.Api.Commands;

public static class UserCommands
{
    #region SaveUser
    public class SaveUserCommand : IntegrationEvent, IRequest<SaveUserResponse>
    {
        public SaveUserCommand(SaveUserRequest user)
        {
            User = user;
        }

        public SaveUserRequest User { get; set; }
    }

    public class SaveUserCommandHandler : IRequestHandler<SaveUserCommand, SaveUserResponse>
    {
        private const string DuplicateEmailMessage = "A user with this email already exists.";

        private readonly ApplicationDbContext _dbContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IDistributedCache _cache;
        private readonly ITokenService _tokenService;
        private readonly IPublishEndpoint _publisher;
        private readonly ILogger<SaveUserCommandHandler> _logger;

        public SaveUserCommandHandler(IServiceProvider serviceProvider)
        {
            _tokenService = serviceProvider.GetRequiredService<ITokenService>();
            // Facultatifs : absents quand aucun broker n'est configuré (EventBusConnection vide).
            _publisher = serviceProvider.GetService<IPublishEndpoint>();
            _logger = serviceProvider.GetService<ILogger<SaveUserCommandHandler>>() ?? NullLogger<SaveUserCommandHandler>.Instance;
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            _mapper = serviceProvider.GetRequiredService<IMapper>();
            _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
            _cache = serviceProvider.GetRequiredService<IDistributedCache>();
        }

        /// <exception cref="ConflictException">L'email est déjà enregistré (HTTP 409).</exception>
        public async Task<SaveUserResponse> Handle(SaveUserCommand command, CancellationToken cancellationToken)
        {
            var entity = _mapper.Map<UserEntity>(command.User);
            // Emails comparés sans casse ni espaces : "Alice@x.com " et "alice@x.com" sont le même compte.
            entity.Email = entity.Email.Trim().ToLowerInvariant();

            if (await _dbContext.Users.AnyAsync(u => u.Email == entity.Email, cancellationToken))
                throw new ConflictException(DuplicateEmailMessage);

            await _unitOfWork.Users.AddAsync(entity, cancellationToken);
            try
            {
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Filet de sécurité : deux inscriptions simultanées passent le AnyAsync mais pas l'index unique.
                throw new ConflictException(DuplicateEmailMessage);
            }

            // La liste mise en cache (GetUsersQuery) est périmée dès qu'un utilisateur est ajouté.
            await _cache.RemoveAsync(UserCacheKeys.AllUsers, cancellationToken);

            await PublishUserCreatedAsync(entity, cancellationToken);

            // Le jeton d'accès n'est délivré qu'ici, une seule fois : il n'est ni stocké ni re-calculable.
            var response = _mapper.Map<SaveUserResponse>(entity);
            response.Signature = _tokenService.Issue(entity.Id, entity.Nickname);
            return response;
        }

        /// <summary>
        /// Annonce « utilisateur créé » aux autres services (Notification…). Publication <b>au mieux</b> : l'utilisateur
        /// est déjà enregistré, une panne du broker ne doit pas faire échouer l'inscription. Le revers : l'événement est
        /// alors perdu (voir « Limites » dans docs/MASSTRANSIT-FLUX.md — la solution est une outbox transactionnelle).
        /// </summary>
        private async Task PublishUserCreatedAsync(UserEntity user, CancellationToken cancellationToken)
        {
            if (_publisher is null)
                return;

            try
            {
                await _publisher.Publish(new UserCreatedContractQuery
                {
                    Email = user.Email,
                    FullName = $"{user.FirstName} {user.LastName}".Trim()
                }, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not publish UserCreated for user {UserId}; the event is lost.", user.Id);
            }
        }
    }
    #endregion
}
