using ConnectLive.Application;
using ConnectLive.Core.Api.Questions;
using ConnectLive.Portal.Shared.Response.Questions;
using ConntectLive.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ConnectLive.Core.Api.Queries;

/// <summary>Lectures des questions. Volontairement non cachées : elles sont propres à chaque auteur et modifiables.</summary>
public static class QuestionQueries
{
    #region GetMyQuestions
    public class GetMyQuestionsQuery : IntegrationEvent, IRequest<List<CreateQuestionResponse>>
    {
        public GetMyQuestionsQuery(Guid ownerId)
        {
            OwnerId = ownerId;
        }

        public Guid OwnerId { get; set; }
    }

    public class GetMyQuestionsQueryHandler : IRequestHandler<GetMyQuestionsQuery, List<CreateQuestionResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetMyQuestionsQueryHandler(IServiceProvider serviceProvider)
        {
            _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        }

        public async Task<List<CreateQuestionResponse>> Handle(GetMyQuestionsQuery request, CancellationToken cancellationToken)
        {
            var questions = await _dbContext.Questions.AsNoTracking()
                .Where(q => q.UserId == request.OwnerId)
                .ToListAsync(cancellationToken);
            return questions.Select(q => q.ToResponse()).ToList();
        }
    }
    #endregion
}
