using LMS.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Api.Endpoints.Assessment;

// ── List groups for an assignment ──────────────────────────────────────────────
public sealed class ListAssignmentGroupsEndpoint(IAssignmentGroupService service) : ApiEndpoint<ListAssignmentGroupsEndpoint.Request, List<AssignmentGroupDto>>
{
    public sealed class Request { [FromRoute] public Guid AssignmentId { get; set; } }

    public override void Configure()
    {
        Get("assignments/{AssignmentId}/groups");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer", "Student");
        Tags("AssignmentGroups");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) =>
        await SendAsync(await service.GetGroupsAsync(req.AssignmentId, ct), ct);
}

// ── Get my group (student view) ────────────────────────────────────────────────
public sealed class GetMyAssignmentGroupEndpoint(IAssignmentGroupService service, ICurrentUserContext userContext) : ApiEndpoint<GetMyAssignmentGroupEndpoint.Request, AssignmentGroupDto?>
{
    public sealed class Request { [FromRoute] public Guid AssignmentId { get; set; } }

    public override void Configure()
    {
        Get("assignments/{AssignmentId}/groups/mine");
        Roles("Student");
        Tags("AssignmentGroups");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userId = await userContext.GetUserIdAsync(ct);
        if (!userId.HasValue) { await SendUnauthorizedAsync(ct); return; }
        var result = await service.GetMyGroupAsync(req.AssignmentId, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

// ── Get enrolled students (for the group picker UI) ────────────────────────────
public sealed class GetEnrolledStudentsForAssignmentEndpoint(IAssignmentGroupService service) : ApiEndpoint<GetEnrolledStudentsForAssignmentEndpoint.Request, List<EnrolledStudentDto>>
{
    public sealed class Request { [FromRoute] public Guid AssignmentId { get; set; } }

    public override void Configure()
    {
        Get("assignments/{AssignmentId}/enrolled-students");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("AssignmentGroups");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) =>
        await SendAsync(await service.GetEnrolledStudentsAsync(req.AssignmentId, ct), ct);
}

// ── Create a group ─────────────────────────────────────────────────────────────
public sealed class CreateAssignmentGroupEndpoint(IAssignmentGroupService service) : ApiEndpoint<CreateAssignmentGroupEndpoint.Request, AssignmentGroupDto>
{
    public sealed class Request : CreateGroupRequest { [FromRoute] public Guid AssignmentId { get; set; } }

    public override void Configure()
    {
        Post("assignments/{AssignmentId}/groups");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("AssignmentGroups");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) =>
        await SendAsync(await service.CreateGroupAsync(req.AssignmentId, req, ct), ct);
}

// ── Update a group ─────────────────────────────────────────────────────────────
public sealed class UpdateAssignmentGroupEndpoint(IAssignmentGroupService service) : ApiEndpoint<UpdateAssignmentGroupEndpoint.Request, AssignmentGroupDto>
{
    public sealed class Request : UpdateGroupRequest { [FromRoute] public Guid GroupId { get; set; } }

    public override void Configure()
    {
        Put("assignments/groups/{GroupId}");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("AssignmentGroups");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) =>
        await SendAsync(await service.UpdateGroupAsync(req.GroupId, req, ct), ct);
}

// ── Delete a group ─────────────────────────────────────────────────────────────
public sealed class DeleteAssignmentGroupEndpoint(IAssignmentGroupService service) : ApiEndpoint<DeleteAssignmentGroupEndpoint.Request, Deleted>
{
    public sealed class Request { [FromRoute] public Guid GroupId { get; set; } }

    public override void Configure()
    {
        Delete("assignments/groups/{GroupId}");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("AssignmentGroups");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) =>
        await SendAsync(await service.DeleteGroupAsync(req.GroupId, ct), ct);
}

// ── Auto-group students ────────────────────────────────────────────────────────
public sealed class AutoGroupStudentsEndpoint(IAssignmentGroupService service) : ApiEndpoint<AutoGroupStudentsEndpoint.Request, List<AssignmentGroupDto>>
{
    public sealed class Request : AutoGroupRequest { [FromRoute] public Guid AssignmentId { get; set; } }

    public override void Configure()
    {
        Post("assignments/{AssignmentId}/groups/auto");
        Roles("SuperAdmin", "Admin", "Instructor", "Lecturer");
        Tags("AssignmentGroups");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct) =>
        await SendAsync(await service.AutoGroupAsync(req.AssignmentId, req, ct), ct);
}
