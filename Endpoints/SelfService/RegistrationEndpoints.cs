using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.SelfService;

public sealed class RegisterForCourseEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateRegistrationRequest, CourseRegistrationDto>
{
    public override void Configure()
    {
        Post("self-service/register");
        Roles("Student", "SuperAdmin");
        Tags("SelfService");
    }

    public override async Task HandleAsync(CreateRegistrationRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Use the authenticated user's ID instead of allowing clients to specify any student ID
        var result = await registrationService.RegisterStudent(userId.Value, req.CourseOfferingId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class DropCourseEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<DropCourseEndpoint.DropCourseRequest, bool>
{
    public class DropCourseRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid EnrollmentId { get; set; }
    }

    public override void Configure()
    {
        Delete("self-service/register/{EnrollmentId}");
        Roles("Student", "SuperAdmin");
        Tags("SelfService");
    }

    public override async Task HandleAsync(DropCourseRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Note: The RegistrationService.DropCourse method doesn't currently validate ownership
        // In a production system, you'd want to add that validation
        var result = await registrationService.DropCourse(userId.Value, req.EnrollmentId, ct);
        if (result.IsError)
        {
            var error = result.FirstError;
            var status = error.Type == ErrorType.NotFound ? 404 : error.Type == ErrorType.Conflict ? 409 : 400;
            await SendFailureAsync(status, error.Description, error.Code, error.Description, ct);
            return;
        }
        await SendSuccessAsync(true, ct);
    }
}

public sealed class GetRegistrationSummaryEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<RegistrationSummaryDto>
{
    public override void Configure()
    {
        Get("self-service/registration-summary");
        Roles("Student", "SuperAdmin");
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
        await SendAsync(await registrationService.GetRegistrationSummaryAsync(userId.Value, sessionId == Guid.Empty ? null : sessionId, ct), ct);
    }
}

public sealed class BulkRegisterRequest
{
    public List<Guid> CourseOfferingIds { get; set; } = new();
}

public sealed class BulkRegisterEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<BulkRegisterRequest, RegistrationSummaryDto>
{
    public override void Configure()
    {
        Post("self-service/register/bulk");
        Roles("Student", "SuperAdmin");
        Tags("SelfService");
    }

    public override async Task HandleAsync(BulkRegisterRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await registrationService.RegisterCoursesBulk(userId.Value, req.CourseOfferingIds, ct);
        if (result.IsError)
        {
            var error = result.FirstError;
            var status = error.Type == ErrorType.NotFound ? 404 : error.Type == ErrorType.Conflict ? 409 : 400;
            await SendFailureAsync(status, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendAsync(result.Value, ct);
    }
}

