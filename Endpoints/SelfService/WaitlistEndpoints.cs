using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.SelfService;

public sealed class JoinWaitlistEndpoint(IWaitlistService waitlistService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<JoinWaitlistRequest, WaitlistDto>
{
    public override void Configure()
    {
        Post("self-service/waitlist/{courseOfferingId:guid}");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(JoinWaitlistRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Use the authenticated user's ID instead of allowing clients to specify any student ID
        var result = await waitlistService.JoinWaitlistAsync(userId.Value, req.CourseOfferingId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class LeaveWaitlistEndpoint(IWaitlistService waitlistService)
    : ApiEndpoint<LeaveWaitlistEndpoint.LeaveWaitlistRequest, bool>
{
    public class LeaveWaitlistRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid WaitlistId { get; set; }
    }

    public override void Configure()
    {
        Delete("self-service/waitlist/{WaitlistId}");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(LeaveWaitlistRequest req, CancellationToken ct)
    {
        var result = await waitlistService.LeaveWaitlistAsync(req.WaitlistId, ct);
        await SendAsync(result.Match(deleted => true, errors => false), ct);
    }
}

public sealed class GetStudentWaitlistsEndpoint(IWaitlistService waitlistService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<WaitlistDto>>
{
    public override void Configure()
    {
        Get("self-service/waitlist");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        Guid.TryParse(sessionIdStr, out var sessionId);
        var academicSessionId = sessionId == Guid.Empty ? (Guid?)null : sessionId;

        var result = await waitlistService.GetStudentWaitlistsAsync(userId.Value, academicSessionId, ct);
        await SendAsync(result, ct);
    }
}
