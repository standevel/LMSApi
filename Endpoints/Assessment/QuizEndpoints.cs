using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Sockets;

namespace LMS.Api.Endpoints.Assessment;

public sealed class CreateQuizEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateQuizRequest, QuizWithSettingsDto>
{
    public override void Configure()
    {
        Post("quizzes");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CreateQuizRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await quizService.CreateQuizWithSettingsAsync(req, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizzesEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<QuizDto>>
{
    public override void Configure()
    {
        Get("quizzes");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var courseOfferingIdRaw = Query<string?>("courseOfferingId", isRequired: false);
        var courseOfferingId = Guid.TryParse(courseOfferingIdRaw, out var parsedCourseOfferingId)
            ? parsedCourseOfferingId
            : (Guid?)null;
        var userId = await currentUserContext.GetUserIdAsync(ct);
        
        var result = await quizService.GetQuizzesByCourseAsync(courseOfferingId, userId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizByIdEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetQuizByIdEndpoint.GetQuizByIdRequest, QuizDto>
{
    public class GetQuizByIdRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizByIdRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await quizService.GetQuizByIdAsync(req.Id, userId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizQuestionsEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetQuizQuestionsEndpoint.GetQuizQuestionsRequest, List<QuizQuestionDto>>
{
    public class GetQuizQuestionsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{QuizId}/questions");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizQuestionsRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await quizService.GetQuestionsForQuizAsync(req.QuizId, userId, ct);
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
        [Microsoft.AspNetCore.Mvc.FromBody] public Guid? AssessmentCategoryId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public List<Guid>? TargetProgramIds { get; set; }
    }

    public override void Configure()
    {
        Put("quizzes/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuizRequest req, CancellationToken ct)
    {
        var result = await quizService.UpdateQuizAsync(req.Id, req.Title, req.Description, req.TimeLimitMinutes, req.AssessmentCategoryId, req.TargetProgramIds, ct);
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
        [Microsoft.AspNetCore.Mvc.FromBody] public int Points { get; set; } = 1;
        [Microsoft.AspNetCore.Mvc.FromBody] public string? Difficulty { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string? Category { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string? Tags { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string? Explanation { get; set; }
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
        var result = await quizService.AddQuestionToQuizAsync(req.QuizId, new CreateQuizQuestionRequest
        {
            QuestionText = req.QuestionText,
            OrderIndex = req.OrderIndex,
            QuestionType = req.QuestionType,
            Points = req.Points,
            Difficulty = req.Difficulty,
            Category = req.Category,
            Tags = req.Tags,
            Explanation = req.Explanation,
            OptionTexts = req.OptionTexts
        }, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DownloadQuizQuestionImportTemplateEndpoint(IQuizService quizService)
    : ApiEndpointWithoutRequest<object>
{
    public override void Configure()
    {
        Get("quizzes/questions/import-template");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await quizService.GenerateQuestionImportTemplateAsync(ct);
        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        var template = result.Value;
        HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{template.FileName}\"";
        HttpContext.Response.ContentType = template.ContentType;
        await HttpContext.Response.Body.WriteAsync(template.FileContent, ct);
        await HttpContext.Response.CompleteAsync();
    }
}

public sealed class PreviewQuizQuestionsImportEndpoint(IQuizService quizService)
    : ApiEndpointWithoutRequest<QuestionImportResultDto>
{
    public override void Configure()
    {
        Post("quizzes/{QuizId:guid}/questions/import/preview");
        Roles("SuperAdmin", "Admin", "Lecturer");
        AllowFileUploads();
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var quizId = Route<Guid>("QuizId");
        var file = HttpContext.Request.Form.Files.FirstOrDefault();
        if (file is null)
        {
            await SendFailureAsync(400, "No file uploaded", "FILE_REQUIRED", "Please upload an Excel file", ct);
            return;
        }

        var result = await quizService.ImportQuizQuestionsAsync(quizId, file, previewOnly: true, ct: ct);
        await SendAsync(result, ct);
    }
}

public sealed class ImportQuizQuestionsEndpoint(IQuizService quizService)
    : ApiEndpointWithoutRequest<QuestionImportResultDto>
{
    public override void Configure()
    {
        Post("quizzes/{QuizId:guid}/questions/import");
        Roles("SuperAdmin", "Admin", "Lecturer");
        AllowFileUploads();
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var quizId = Route<Guid>("QuizId");
        var file = HttpContext.Request.Form.Files.FirstOrDefault();
        if (file is null)
        {
            await SendFailureAsync(400, "No file uploaded", "FILE_REQUIRED", "Please upload an Excel file", ct);
            return;
        }

        var result = await quizService.ImportQuizQuestionsAsync(quizId, file, previewOnly: false, ct: ct);
        await SendAsync(result, ct);
    }
}

public sealed class AddQuestionsFromBankEndpoint(IQuizService quizService)
    : ApiEndpoint<AddQuestionsFromBankEndpoint.AddQuestionsFromBankEndpointRequest, List<QuizQuestionDto>>
{
    public class AddQuestionsFromBankEndpointRequest : AddQuestionsFromBankRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Post("quizzes/{QuizId}/questions/from-bank");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(AddQuestionsFromBankEndpointRequest req, CancellationToken ct)
    {
        var result = await quizService.AddQuestionsFromBankAsync(req.QuizId, req, ct);
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

public sealed class StartQuizAttemptEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext, IWebHostEnvironment environment)
    : ApiEndpoint<StartQuizAttemptRequest, QuizAttemptDto>
{
    public override void Configure()
    {
        Post("quizzes/{quizId:guid}/attempts");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(StartQuizAttemptRequest req, CancellationToken ct)
    {
        var quizId = Route<Guid>("quizId");
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var clientIps = ResolveClientIpCandidates(HttpContext.Connection.RemoteIpAddress, environment);
        var result = await quizService.StartNewQuizAttemptAsync(userId.Value, quizId, req.AccessCode, clientIps, ct);
        await SendAsync(result, ct);
    }

    private static IReadOnlyCollection<string> ResolveClientIpCandidates(IPAddress? remoteIpAddress, IWebHostEnvironment environment)
    {
        var candidates = new List<string>();
        if (remoteIpAddress is not null)
        {
            candidates.Add(remoteIpAddress.ToString());
        }

        if (environment.IsDevelopment() && remoteIpAddress is not null && IPAddress.IsLoopback(remoteIpAddress))
        {
            try
            {
                candidates.AddRange(Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                    .Select(address => address.ToString()));
            }
            catch
            {
                // Best-effort development fallback only.
            }
        }

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class SubmitQuizAnswersEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<SubmitQuizAnswersEndpoint.SubmitQuizAnswersRequest, QuizResultDto>
{
    public class SubmitQuizAnswersRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid AttemptId { get; set; }
        // Angular sends { answers: {"guid": "value"} } — string keys, case-insensitive JSON binding
        public Dictionary<string, string> Answers { get; set; } = new();
    }

    public override void Configure()
    {
        Put("quizzes/attempts/{AttemptId}/answers");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(SubmitQuizAnswersRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Convert string keys to Guid for the service layer
        var answers = req.Answers
            .Where(kv => Guid.TryParse(kv.Key, out _))
            .ToDictionary(kv => Guid.Parse(kv.Key), kv => kv.Value);
        var result = await quizService.SubmitAnswersForGradingAsync(req.AttemptId, answers, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizAttemptEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
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
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await quizService.GetQuizAttemptAsync(req.Id, userId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizAttemptResultsEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetQuizAttemptResultsEndpoint.GetQuizAttemptResultsRequest, QuizResultDto>
{
    public class GetQuizAttemptResultsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/attempts/{Id}/results");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizAttemptResultsRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await quizService.GetQuizResultAsync(req.Id, userId, ct);
        await SendAsync(result, ct);
    }
}

// ==================== Question Option Management ====================

public sealed class AddQuestionOptionEndpoint(IQuizService quizService)
    : ApiEndpoint<AddQuestionOptionEndpoint.AddQuestionOptionRequest, QuestionOptionDto>
{
    public class AddQuestionOptionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuestionId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string OptionText { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public int DisplayOrder { get; set; }
    }

    public override void Configure()
    {
        Post("quizzes/questions/{QuestionId}/options");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(AddQuestionOptionRequest req, CancellationToken ct)
    {
        var result = await quizService.AddOptionToQuestionAsync(req.QuestionId, req.OptionText, req.DisplayOrder, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuestionOptionEndpoint(IQuizService quizService)
    : ApiEndpoint<UpdateQuestionOptionEndpoint.UpdateQuestionOptionRequest, QuestionOptionDto>
{
    public class UpdateQuestionOptionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string OptionText { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public bool IsCorrectAnswer { get; set; }
    }

    public override void Configure()
    {
        Put("quizzes/options/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuestionOptionRequest req, CancellationToken ct)
    {
        var result = await quizService.UpdateOptionAsync(req.Id, req.OptionText, req.IsCorrectAnswer, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteQuestionOptionEndpoint(IQuizService quizService)
    : ApiEndpoint<DeleteQuestionOptionEndpoint.DeleteQuestionOptionRequest, Deleted>
{
    public class DeleteQuestionOptionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("quizzes/options/{Id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(DeleteQuestionOptionRequest req, CancellationToken ct)
    {
        var result = await quizService.DeleteOptionAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

public sealed class SetCorrectOptionEndpoint(IQuizService quizService)
    : ApiEndpoint<SetCorrectOptionEndpoint.SetCorrectOptionRequest, Deleted>
{
    public class SetCorrectOptionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuestionId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public Guid OptionId { get; set; }
    }

    public override void Configure()
    {
        Put("quizzes/questions/{QuestionId}/correct-option");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(SetCorrectOptionRequest req, CancellationToken ct)
    {
        var result = await quizService.SetCorrectOptionAsync(req.QuestionId, req.OptionId, ct);
        await SendAsync(result, ct);
    }
}

// ==================== Quiz Analytics ====================

public sealed class GetQuizAttemptsEndpoint(IQuizService quizService)
    : ApiEndpoint<GetQuizAttemptsEndpoint.GetQuizAttemptsRequest, List<QuizAttemptWithStudentDto>>
{
    public class GetQuizAttemptsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{QuizId}/attempts");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizAttemptsRequest req, CancellationToken ct)
    {
        var result = await quizService.GetQuizAttemptsAsync(req.QuizId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetQuizStatisticsEndpoint(IQuizService quizService)
    : ApiEndpoint<GetQuizStatisticsEndpoint.GetQuizStatisticsRequest, QuizStatisticsDto>
{
    public class GetQuizStatisticsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{QuizId}/statistics");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizStatisticsRequest req, CancellationToken ct)
    {
        var result = await quizService.GetQuizStatisticsAsync(req.QuizId, ct);
        await SendAsync(result, ct);
    }
}

// ==================== Quiz Feedback ====================

public sealed class GetQuizFeedbacksEndpoint(IQuizService quizService)
    : ApiEndpoint<GetQuizFeedbacksEndpoint.GetQuizFeedbacksRequest, List<QuizFeedbackDto>>
{
    public class GetQuizFeedbacksRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{QuizId}/feedback");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizFeedbacksRequest req, CancellationToken ct)
    {
        var result = await quizService.GetQuizFeedbacksAsync(req.QuizId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class CreateQuizFeedbackEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateQuizFeedbackEndpoint.CreateQuizFeedbackRequest, QuizFeedbackDto>
{
    public class CreateQuizFeedbackRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public Guid StudentId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public Guid? QuestionId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string FeedbackText { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public string FeedbackType { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromBody] public string? GradingNotes { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public decimal? ManualOverrideScore { get; set; }
    }

    public override void Configure()
    {
        Post("quizzes/{QuizId}/feedback");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CreateQuizFeedbackRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await quizService.CreateQuizFeedbackAsync(req.QuizId, req.StudentId, req.QuestionId, req.FeedbackText, req.FeedbackType, req.GradingNotes, req.ManualOverrideScore, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuizFeedbackEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateQuizFeedbackEndpoint.UpdateQuizFeedbackRequest, Deleted>
{
    public class UpdateQuizFeedbackRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string? FeedbackText { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string? GradingNotes { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public decimal? ManualOverrideScore { get; set; }
    }

    public override void Configure()
    {
        Put("feedbacks/{Id}");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuizFeedbackRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await quizService.UpdateQuizFeedbackAsync(req.Id, req.FeedbackText, req.GradingNotes, req.ManualOverrideScore, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DeleteQuizFeedbackEndpoint(IQuizService quizService)
    : ApiEndpoint<DeleteQuizFeedbackEndpoint.DeleteQuizFeedbackRequest, Deleted>
{
    public class DeleteQuizFeedbackRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("feedbacks/{Id}");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(DeleteQuizFeedbackRequest req, CancellationToken ct)
    {
        var result = await quizService.DeleteQuizFeedbackAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

// ==================== Time Extension ====================

public sealed class GetQuizTimeExtensionsEndpoint(IQuizService quizService)
    : ApiEndpoint<GetQuizTimeExtensionsEndpoint.GetQuizTimeExtensionsRequest, List<TimeExtensionDto>>
{
    public class GetQuizTimeExtensionsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{QuizId}/time-extensions");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizTimeExtensionsRequest req, CancellationToken ct)
    {
        var result = await quizService.GetQuizTimeExtensionsAsync(req.QuizId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class CreateTimeExtensionEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateTimeExtensionEndpoint.CreateTimeExtensionRequest, TimeExtensionDto>
{
    public class CreateTimeExtensionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public Guid StudentId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public int AdditionalMinutes { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public DateTime? EffectiveFrom { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public DateTime? EffectiveUntil { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Reason { get; set; } = default!;
    }

    public override void Configure()
    {
        Post("quizzes/{QuizId}/time-extensions");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CreateTimeExtensionRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await quizService.CreateTimeExtensionAsync(req.QuizId, req.StudentId, req.AdditionalMinutes, req.EffectiveFrom, req.EffectiveUntil, req.Reason, userId.Value.ToString(), ct);
        await SendAsync(result, ct);
    }
}

public sealed class RevokeTimeExtensionEndpoint(IQuizService quizService)
    : ApiEndpoint<RevokeTimeExtensionEndpoint.RevokeTimeExtensionRequest, Deleted>
{
    public class RevokeTimeExtensionRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("time-extensions/{Id}");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(RevokeTimeExtensionRequest req, CancellationToken ct)
    {
        var result = await quizService.RevokeTimeExtensionAsync(req.Id, ct);
        await SendAsync(result, ct);
    }
}

// ==================== Quiz with Settings ====================

public sealed class GetQuizWithSettingsEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetQuizWithSettingsEndpoint.GetQuizWithSettingsRequest, QuizWithSettingsDto>
{
    public class GetQuizWithSettingsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("quizzes/{Id}/with-settings");
        Roles("SuperAdmin", "Admin", "Lecturer", "Student");
        Tags("Assessment");
    }

    public override async Task HandleAsync(GetQuizWithSettingsRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await quizService.GetQuizWithSettingsAsync(req.Id, userId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuizSettingsEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateQuizSettingsEndpoint.UpdateQuizSettingsRequest, QuizSettingDto>
{
    public class UpdateQuizSettingsRequest : UpdateQuizSettingRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid QuizId { get; set; }
    }

    public override void Configure()
    {
        Put("quizzes/{QuizId}/settings");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuizSettingsRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await quizService.UpdateQuizSettingsAsync(req.QuizId, req, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateQuizStatusEndpoint(IQuizService quizService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateQuizStatusEndpoint.UpdateQuizStatusRequest, Deleted>
{
    public class UpdateQuizStatusRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public override void Configure()
    {
        Patch("quizzes/{Id:guid}/status");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateQuizStatusRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await quizService.UpdateQuizStatusAsync(req.Id, req.Status, userId.Value, ct);
        await SendAsync(result, ct);
    }
}
