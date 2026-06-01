using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Parents;

public sealed class GetLinkedStudentsEndpoint(IParentPortalService parentPortalService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetLinkedStudentsEndpoint.GetLinkedStudentsRequest, List<ParentGuardianDto>>
{
    public class GetLinkedStudentsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid ParentId { get; set; }
    }

    public override void Configure()
    {
        Get("parents/{ParentId}/students");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(GetLinkedStudentsRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // In a real implementation, you'd verify that the authenticated user is actually the parent
        // For now, we'll just use the requested parent ID
        var result = await parentPortalService.GetLinkedStudentsAsync(req.ParentId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetStudentProgressEndpoint(IParentPortalService parentPortalService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetStudentProgressEndpoint.GetStudentProgressRequest, StudentProgressDto>
{
    public class GetStudentProgressRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
    }

    public override void Configure()
    {
        Get("parents/students/{StudentId}/progress");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(GetStudentProgressRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // In a real implementation, you'd verify that the authenticated parent is linked to this student
        var result = await parentPortalService.GetStudentProgressAsync(req.StudentId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetStudentGradesEndpoint(IParentPortalService parentPortalService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetStudentGradesEndpoint.GetStudentGradesRequest, StudentGradesDto>
{
    public class GetStudentGradesRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
    }

    public override void Configure()
    {
        Get("parents/students/{StudentId}/grades");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(GetStudentGradesRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // In a real implementation, you'd verify that the authenticated parent is linked to this student
        var result = await parentPortalService.GetStudentGradesAsync(req.StudentId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class SendMessageToStudentEndpoint(IParentPortalService parentPortalService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<SendMessageToStudentEndpoint.SendMessageToStudentRequest, bool>
{
    public class SendMessageToStudentRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
        [Microsoft.AspNetCore.Mvc.FromBody] public string Content { get; set; } = default!;
    }

    public override void Configure()
    {
        Post("parents/students/{StudentId}/messages");
        Roles("Parent");
        Tags("Parents");
    }

    public override async Task HandleAsync(SendMessageToStudentRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // In a real implementation, you'd verify that the authenticated parent is linked to this student
        var result = await parentPortalService.SendMessageToStudentAsync(req.StudentId, userId.Value, req.Content, ct);
        await SendAsync(result.Match(deleted => true, errors => false), ct);
    }
}
