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

public sealed class UpdateProctoringHeartbeatEndpoint(IProctoringService proctoringService)
    : ApiEndpoint<ProctoringHeartbeatRequest, ExamProctoringSessionDto>
{
    public override void Configure()
    {
        Put("exams/proctoring/{SessionId:guid}/heartbeat");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
        Tags("Assessment");
    }

    public override async Task HandleAsync(ProctoringHeartbeatRequest req, CancellationToken ct)
    {
        var result = await proctoringService.UpdateProctoringHeartbeatAsync(req.SessionId, req.HeartbeatTimeUtc, req.UserIPAddress, ct);
        await SendAsync(result, ct);
    }
}