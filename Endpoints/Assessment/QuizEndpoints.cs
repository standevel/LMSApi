using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Assessment;

public sealed class CreateQuizEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateQuizRequest, QuizDto>
{
    public override void Configure()
    {
        Post("quizzes");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CreateQuizRequest req, CancellationToken ct)
    {
        var result = await quizService.CreateQuizAsync(req.CourseOfferingId, req.Title, req.Description, req.TimeLimitMinutes, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizzesEndpoint(IQuizService quizService)
    : ApiEndpointWithoutRequest<List<QuizDto>>
{
    public override void Configure()
    {
        Get("quizzes");
        AllowAnonymous();
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var courseOfferingId = QueryParam<Guid>("courseOfferingId");
        var result = await quizService.GetQuizzesByCourseAsync(courseOfferingId ?? Guid.Empty, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizByIdEndpoint(IQuizService quizService)
    : ApiEndpoint<GetQuizByIdEndpoint.GetQuizByIdRequest, QuizDto>
{
    public class GetQuizByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{Id}");
        AllowAnonymous();
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizByIdRequest req, CancellationToken ct)
    {
        var result = await quizService.GetQuizByIdAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizQuestionsEndpoint(IQuizService quizService)
    : ApiEndpoint<GetQuizQuestionsEndpoint.GetQuizQuestionsRequest, List<QuizQuestionDto>>
{
    public class GetQuizQuestionsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{QuizId}/questions");
        AllowAnonymous();
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizQuestionsRequest req, CancellationToken ct)
    {
        var result = await quizService.GetQuestionsForQuizAsync(req.QuizId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuizEndpoint(IQuizService quizService)
    : ApiEndpoint<UpdateQuizEndpoint.UpdateQuizRequest, Deleted>
{
    public class UpdateQuizRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Title { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public string Description { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public int TimeLimitMinutes { get; set; }
    }

    public override void Configure()
    {
        Put("quizzes/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuizRequest req, CancellationToken ct)
    {
        var result = await quizService.UpdateQuizAsync(req.Id, req.Title, req.Description, req.TimeLimitMinutes, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteQuizEndpoint(IQuizService quizService)
    : ApiEndpoint<DeleteQuizEndpoint.DeleteQuizRequest, Deleted>
{
    public class DeleteQuizRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("quizzes/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(DeleteQuizRequest req, CancellationToken ct)
    {
        var result = await quizService.DeleteQuizAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class AddQuizQuestionEndpoint(IQuizService quizService)
    : ApiEndpoint<AddQuizQuestionEndpoint.AddQuizQuestionRequest, QuizQuestionDto>
{
    public class AddQuizQuestionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string QuestionText { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public int OrderIndex { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string QuestionType { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public List<string> OptionTexts { get; set; } = new();
    }

    public override void Configure()
    {
        Post("quizzes/{QuizId}/questions");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(AddQuizQuestionRequest req, CancellationToken ct)
    {
        var result = await quizService.AddQuestionToQuizAsync(req.QuizId, req.QuestionText, req.OrderIndex, req.QuestionType, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuizQuestionEndpoint(IQuizService quizService)
    : ApiEndpoint<UpdateQuizQuestionEndpoint.UpdateQuizQuestionRequestEndpoint, QuizQuestionDto>
{
    public class UpdateQuizQuestionRequestEndpoint
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string QuestionText { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public int OrderIndex { get; set; }
    }

    public override void Configure()
    {
        Put("quizzes/questions/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuizQuestionRequestEndpoint req, CancellationToken ct)
    {
        var result = await quizService.UpdateQuizQuestionAsync(req.Id, req.QuestionText, req.OrderIndex, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteQuizQuestionEndpoint(IQuizService quizService)
    : ApiEndpoint<DeleteQuizQuestionEndpoint.DeleteQuizQuestionRequest, bool>
{
    public class DeleteQuizQuestionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("quizzes/questions/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(DeleteQuizQuestionRequest req, CancellationToken ct)
    {
        var result = await quizService.DeleteQuizQuestionAsync(req.Id, ct);
        await SendAsync(result.Match(deleted => true, errors => false), ct);
    }
}

public sealed class StartQuizAttemptEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<QuizAttemptDto>
{
    public override void Configure()
    {
        Post("quizzes/{quizId:guid}/attempts");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var quizId = Route<Guid>("quizId");
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await quizService.StartNewQuizAttemptAsync(userId.Value, quizId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class SubmitQuizAnswersEndpoint(IQuizService quizService)
    : ApiEndpoint<SubmitQuizAnswersEndpoint.SubmitQuizAnswersRequest, QuizResultDto>
{
    public class SubmitQuizAnswersRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid AttemptId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public Dictionary<Guid, string> Answers { get; set; } = new();
    }

    public override void Configure()
    {
        Put("quizzes/attempts/{AttemptId}/answers");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(SubmitQuizAnswersRequest req, CancellationToken ct)
    {
        var result = await quizService.SubmitAnswersForGradingAsync(req.AttemptId, req.Answers, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizAttemptEndpoint(IQuizService quizService)
    : ApiEndpoint<GetQuizAttemptEndpoint.GetQuizAttemptRequest, QuizAttemptDto>
{
    public class GetQuizAttemptRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/attempts/{Id}");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizAttemptRequest req, CancellationToken ct)
    {
        var result = await quizService.GetQuizAttemptAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}
