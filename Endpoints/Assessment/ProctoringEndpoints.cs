using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Assessment;

public sealed class StartProctoringSessionEndpoint(IProctoringService proctoringService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<ExamProctoringSessionDto>
{
    public override void Configure()
    {
        Post("exams/{quizId:guid}/proctoring");
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

        var result = await proctoringService.StartProctoringSessionAsync(userId.Value, quizId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class StartProctoringSessionWithDetailsEndpoint(IProctoringService proctoringService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<StartProctoringRequest, ExamProctoringSessionDto>
{
    public override void Configure()
    {
        Post("exams/{quizId:guid}/proctoring/start");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(StartProctoringRequest req, CancellationToken ct)
    {
        var quizId = Route<Guid>("quizId");
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await proctoringService.StartProctoringSessionAsync(userId.Value, quizId, req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class UpdateProctoringHeartbeatEndpoint(IProctoringService proctoringService)
    : ApiEndpoint<UpdateProctoringHeartbeatRequest, ExamProctoringSessionDto>
{
    public override void Configure()
    {
        Put("exams/proctoring/{SessionId:guid}/heartbeat");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(UpdateProctoringHeartbeatRequest req, CancellationToken ct)
    {
        var result = await proctoringService.UpdateProctoringHeartbeatAsync(req.SessionId, req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class RecordProctoringViolationEndpoint(IProctoringService proctoringService)
    : ApiEndpoint<RecordViolationRequest, ExamProctoringSessionDto>
{
    public override void Configure()
    {
        Post("exams/proctoring/violation");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(RecordViolationRequest req, CancellationToken ct)
    {
        var result = await proctoringService.RecordViolationAsync(req.SessionId, req, ct);
        await SendAsync(result, ct);
    }
}

public sealed class EndProctoringSessionEndpoint(IProctoringService proctoringService)
    : ApiEndpointWithoutRequest<ExamProctoringSessionDto>
{
    public override void Configure()
    {
        Post("exams/proctoring/{sessionId:guid}/end");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("sessionId");
        var result = await proctoringService.EndProctoringSessionAsync(sessionId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetProctoringSessionEndpoint(IProctoringService proctoringService)
    : ApiEndpointWithoutRequest<ProctoringSessionDto>
{
    public override void Configure()
    {
        Get("exams/proctoring/{sessionId:guid}");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("sessionId");
        var result = await proctoringService.GetSessionAsync(sessionId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetProctoringLecturerDashboardEndpoint(IProctoringService proctoringService)
    : ApiEndpointWithoutRequest<ProctoringLecturerDto>
{
    public override void Configure()
    {
        Get("exams/proctoring/lecturer/{quizId:guid}");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var quizId = Route<Guid>("quizId");
        var result = await proctoringService.GetLecturerDashboardAsync(quizId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetProctoringSessionsByQuizEndpoint(IProctoringService proctoringService)
    : ApiEndpointWithoutRequest<List<ProctoringSessionDto>>
{
    public override void Configure()
    {
        Get("exams/proctoring/quiz/{quizId:guid}");
        Roles("Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var quizId = Route<Guid>("quizId");
        var result = await proctoringService.GetSessionsByQuizAsync(quizId, ct);
        await SendAsync(result, ct);
    }
}

public class ListProctoringSessionsRequest
{
    public Guid? QuizId { get; set; }
}

public sealed class ListProctoringSessionsEndpoint(IProctoringService proctoringService)
    : ApiEndpoint<ListProctoringSessionsRequest, List<ExamProctoringSessionDto>>
{
    public override void Configure()
    {
        Get("exams/proctoring");
        Roles("Student", "Lecturer", "SuperAdmin", "Admin");
        Tags("Assessment");
    }

    public override async Task HandleAsync(ListProctoringSessionsRequest req, CancellationToken ct)
    {
        var result = await proctoringService.ListSessionsAsync(req.QuizId, ct);
        await SendAsync(result, ct);
    }
}