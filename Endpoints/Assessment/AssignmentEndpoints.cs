using LMS.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Api.Endpoints.Assessment;

public sealed class CreateAssignmentEndpoint(IAssignmentService service, ICurrentUserContext userContext) : ApiEndpoint<UpsertAssignmentRequest, AssignmentDto>
{
    public override void Configure()
    {
        Post("assignments");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("Assignments");
    }

    public override async Task HandleAsync(UpsertAssignmentRequest req, CancellationToken ct)
    {
        var userId = await userContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }
        await SendAsync(await service.CreateAssignmentAsync(req, userId.Value, ct), ct);
    }
}

public sealed class ListAssignmentsEndpoint(IAssignmentService service, ICurrentUserContext userContext) : ApiEndpointWithoutRequest<List<AssignmentDto>>
{
    public override void Configure()
    {
        Get("assignments");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer", "Student");
        Tags("Assignments");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var raw = Query<string?>("courseOfferingId", isRequired: false);
        var courseOfferingId = Guid.TryParse(raw, out var parsed) ? parsed : (Guid?)null;
        var isStudentOnly = User.IsInRole("Student")
            && !User.IsInRole("Admin")
            && !User.IsInRole("SuperAdmin")
            && !User.IsInRole("Instructor")
            && !User.IsInRole("Lecturer");
        var userId = isStudentOnly ? await userContext.GetUserIdAsync(ct) : null;
        await SendAsync(await service.GetAssignmentsAsync(courseOfferingId, userId, isStudentOnly, ct), ct);
    }
}

public sealed class GetAssignmentEndpoint(IAssignmentService service) : ApiEndpoint<GetAssignmentEndpoint.Request, AssignmentDto>
{
    public sealed class Request
    {
        [FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Get("assignments/{Id}");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer", "Student");
        Tags("Assignments");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) => await SendAsync(await service.GetAssignmentAsync(req.Id, ct), ct);
}

public sealed class UpdateAssignmentEndpoint(IAssignmentService service) : ApiEndpoint<UpdateAssignmentEndpoint.Request, AssignmentDto>
{
    public sealed class Request : UpsertAssignmentRequest
    {
        [FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Put("assignments/{Id}");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("Assignments");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) => await SendAsync(await service.UpdateAssignmentAsync(req.Id, req, ct), ct);
}

public sealed class DeleteAssignmentEndpoint(IAssignmentService service) : ApiEndpoint<DeleteAssignmentEndpoint.Request, Deleted>
{
    public sealed class Request
    {
        [FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("assignments/{Id}");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("Assignments");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) => await SendAsync(await service.DeleteAssignmentAsync(req.Id, ct), ct);
}

public sealed class SubmitAssignmentEndpoint(IAssignmentService service, ICurrentUserContext userContext) : ApiEndpoint<SubmitAssignmentRequest, AssignmentSubmissionDto>
{
    public override void Configure()
    {
        Post("assignments/submissions");
        Roles("Student", "SuperAdmin");
        Tags("Assignments");
    }

    public override async Task HandleAsync(SubmitAssignmentRequest req, CancellationToken ct)
    {
        var userId = await userContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }
        await SendAsync(await service.SubmitAsync(req, userId.Value, ct), ct);
    }
}

public sealed class ListAssignmentSubmissionsEndpoint(IAssignmentService service, ICurrentUserContext userContext) : ApiEndpoint<ListAssignmentSubmissionsEndpoint.Request, List<AssignmentSubmissionDto>>
{
    public sealed class Request
    {
        [FromRoute] public Guid AssignmentId { get; set; }
    }

    public override void Configure()
    {
        Get("assignments/{AssignmentId}/submissions");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer", "Student");
        Tags("Assignments");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var isStudent = User.IsInRole("Student") && !User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("Instructor") && !User.IsInRole("Lecturer");
        var submitterId = isStudent ? await userContext.GetUserIdAsync(ct) : null;
        if (isStudent && !submitterId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }
        await SendAsync(await service.GetSubmissionsAsync(req.AssignmentId, submitterId, ct), ct);
    }
}

public sealed class GradeAssignmentSubmissionEndpoint(IAssignmentService service, ICurrentUserContext userContext) : ApiEndpoint<GradeSubmissionRequest, AssignmentSubmissionDto>
{
    public override void Configure()
    {
        Post("assignments/submissions/grade");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("Assignments");
    }

    public override async Task HandleAsync(GradeSubmissionRequest req, CancellationToken ct)
    {
        var graderId = await userContext.GetUserIdAsync(ct);
        if (!graderId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }
        await SendAsync(await service.GradeAsync(req, graderId.Value, ct), ct);
    }
}

public record ImportAssignmentsRequest(Guid SourceOfferingId, Guid TargetOfferingId);

public sealed class ImportAssignmentsFromOfferingEndpoint(IAssignmentService service, ICurrentUserContext userContext) : ApiEndpoint<ImportAssignmentsRequest, int>
{
    public override void Configure()
    {
        Post("assignments/import-from-offering");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("Assignments");
    }

    public override async Task HandleAsync(ImportAssignmentsRequest req, CancellationToken ct)
    {
        var userId = await userContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var result = await service.ImportAssignmentsFromOfferingAsync(req.SourceOfferingId, req.TargetOfferingId, userId.Value, ct);
        await SendAsync(result, ct);
    }
}
