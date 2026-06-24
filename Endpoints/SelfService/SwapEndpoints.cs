using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.SelfService;

public sealed class GetCourseSwapOptionsEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<CourseSwapOptionsDto>
{
    public override void Configure()
    {
        Get("self-service/swap-options");
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

        var result = await registrationService.GetCourseSwapOptionsAsync(userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class RequestCourseSwapEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateCourseSwapRequest, CourseSwapRequestDto>
{
    public override void Configure()
    {
        Post("self-service/swap");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(CreateCourseSwapRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await registrationService.RequestCourseSwapAsync(
            userId.Value,
            req.CurrentCourseOfferingId,
            req.NewCourseOfferingId,
            ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetSwapRequestsEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<CourseSwapRequestDto>>
{
    public override void Configure()
    {
        Get("self-service/swap-requests");
        Roles("Student", "SuperAdmin", "Admin", "Lecturer");
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

        var studentId = User.IsInRole("Student") ? userId : null;
        var result = await registrationService.GetSwapRequestsAsync(studentId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class ApproveSwapRequestEndpoint(IRegistrationService registrationService)
    : ApiEndpoint<ApproveSwapRequestEndpoint.ApproveSwapRequest, bool>
{
    public class ApproveSwapRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Put("self-service/swap-requests/{Id}/approve");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("SelfService");
    }

    public override async Task HandleAsync(ApproveSwapRequest req, CancellationToken ct)
    {
        // Note: The RegistrationService doesn't currently have a ProcessSwapRequest method
        // This would need to be implemented in the service
        await SendFailureAsync(501, "Not Implemented", "NOT_IMPLEMENTED", "Approve swap request functionality not yet implemented", ct);
    }
}
