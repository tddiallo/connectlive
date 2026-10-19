using ConnectLive.Application;
using ConnectLive.Core.Api.Questions;
using ConnectLive.Portal.Shared.Request.Questions;
using ConnectLive.Portal.Shared.Response.Questions;
using ConntectLive.DAL;
using MediatR;

namespace ConnectLive.Core.Api.Commands;

public static class QuestionCommands
{
    #region CreateQuestion
    public class CreateQuestionCommand : IntegrationEvent, IRequest<CreateQuestionResponse>
    {
        /// <param name="question">Contenu de la question.</param>
        /// <param name="ownerId">Utilisateur authentifié : il en est l'auteur.</param>
        public CreateQuestionCommand(CreateQuestionRequest question, Guid ownerId)
        {
            Question = question;
            OwnerId = ownerId;
        }

        public CreateQuestionRequest Question { get; set; }

        public Guid OwnerId { get; set; }
    }

    public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, CreateQuestionResponse>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateQuestionCommandHandler(IServiceProvider serviceProvider)
        {
            _unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        }

        public async Task<CreateQuestionResponse> Handle(CreateQuestionCommand command, CancellationToken cancellationToken)
        {
            var request = command.Question;
            var entity = new QuestionEntity
            {
                // L'identifiant est celui généré par l'entité : jamais celui proposé par le client
                UserId = command.OwnerId,
                Question = request.Question.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
                Choices = request.Choices.ToList(),
                CorrectAnswer = request.CorrectAnswer
            };

            await _unitOfWork.Questions.AddAsync(entity, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return entity.ToResponse();
        }
    }
    #endregion
}
