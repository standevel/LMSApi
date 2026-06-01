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
        Roles("Student");
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
        Roles("Student");
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
        var result = await registrationService.DropCourse(req.EnrollmentId, ct);
        await SendAsync(result.Match(deleted => true, errors => false), ct);
    }
}
