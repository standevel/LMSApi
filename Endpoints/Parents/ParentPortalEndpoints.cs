using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Parents;

public sealed class GetLinkedStudentsEndpoint(
    IParentPortalService parentPortalService, 
    ICurrentUserContext currentUserContext,
    LmsDbContext dbContext)
    : ApiEndpoint<GetLinkedStudentsEndpoint.GetLinkedStudentsRequest, List<ParentStudentLinkDto>>
{
    public class GetLinkedStudentsRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public string ParentId { get; set; } = string.Empty;
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

        Guid parentGuid;
        if (req.ParentId == "current-parent-id" || req.ParentId == "me" || !Guid.TryParse(req.ParentId, out parentGuid))
        {
            var parentGuardian = await dbContext.ParentGuardians
                .FirstOrDefaultAsync(pg => pg.UserId == userId.Value, ct);

            if (parentGuardian == null)
            {
                await SendSuccessAsync(new List<ParentStudentLinkDto>(), ct);
                return;
            }
            parentGuid = parentGuardian.Id;
        }

        var result = await parentPortalService.GetLinkedStudentsAsync(parentGuid, ct);
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
        public string? Subject { get; set; }
        public string Content { get; set; } = string.Empty;
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

        var content = string.IsNullOrWhiteSpace(req.Subject)
            ? req.Content
            : $"{req.Subject.Trim()}\n\n{req.Content}";
        var result = await parentPortalService.SendMessageToStudentAsync(req.StudentId, userId.Value, content, ct);
        await SendAsync(result, ct);
    }
}
