using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services.AI.Tools;

namespace LMS.Api.Endpoints.AI;

public record GenerateQuizRequest(string Topic, string Difficulty = "Intermediate", int Count = 4);
public record GenerateQuizResponse(string QuizText);

public class GenerateQuizEndpoint(LecturerCopilotTools tools) : ApiEndpoint<GenerateQuizRequest, GenerateQuizResponse>
{
    public override void Configure()
    {
        Post("ai/lecturer/generate-quiz");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Generates CBT exam & quiz questions for course topics";
            s.Description = "Generates Bloom's taxonomy aligned multiple choice & essay questions.";
        });
    }

    public override async Task HandleAsync(GenerateQuizRequest req, CancellationToken ct)
    {
        var result = tools.GenerateQuizQuestions(req.Topic, req.Difficulty, req.Count);
        await SendSuccessAsync(new GenerateQuizResponse(result), ct, "Quiz generated successfully.");
    }
}

public record DraftFeedbackRequest(string SubmissionText, string RubricCriteria = "Clarity, Depth, Structure");
public record DraftFeedbackResponse(string FeedbackText);

public class DraftFeedbackEndpoint(LecturerCopilotTools tools) : ApiEndpoint<DraftFeedbackRequest, DraftFeedbackResponse>
{
    public override void Configure()
    {
        Post("ai/lecturer/draft-feedback");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Evaluates student submission and drafts lecturer feedback";
        });
    }

    public override async Task HandleAsync(DraftFeedbackRequest req, CancellationToken ct)
    {
        var result = tools.DraftEssayFeedback(req.SubmissionText, req.RubricCriteria);
        await SendSuccessAsync(new DraftFeedbackResponse(result), ct, "Feedback drafted successfully.");
    }
}

public record AtRiskStudentsRequest(Guid OfferingId);
public record AtRiskStudentsResponse(string ReportText);

public class AtRiskStudentsEndpoint(LecturerCopilotTools tools) : ApiEndpoint<AtRiskStudentsRequest, AtRiskStudentsResponse>
{
    public override void Configure()
    {
        Post("ai/lecturer/at-risk-students");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Identifies at-risk students for early intervention";
        });
    }

    public override async Task HandleAsync(AtRiskStudentsRequest req, CancellationToken ct)
    {
        var result = await tools.IdentifyAtRiskStudentsAsync(req.OfferingId, ct);
        await SendSuccessAsync(new AtRiskStudentsResponse(result), ct, "At-risk report compiled.");
    }
}

public record SenateReportRequest(Guid OfferingId);
public record SenateReportResponse(string ReportText);

public class SenateReportEndpoint(LecturerCopilotTools tools) : ApiEndpoint<SenateReportRequest, SenateReportResponse>
{
    public override void Configure()
    {
        Post("ai/lecturer/senate-report");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Generates formal Senate academic course report";
        });
    }

    public override async Task HandleAsync(SenateReportRequest req, CancellationToken ct)
    {
        var result = await tools.GenerateSenateCourseReportAsync(req.OfferingId, ct);
        await SendSuccessAsync(new SenateReportResponse(result), ct, "Senate report generated.");
    }
}
