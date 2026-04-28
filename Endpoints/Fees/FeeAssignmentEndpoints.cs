using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Fees;

public sealed class AssignFeeEndpoint(IFeeService feeService)
    : ApiEndpoint<AssignFeeRequest, FeeAssignmentResponse>
{
    public override void Configure()
    {
        Post("/api/fees/assignments");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(AssignFeeRequest req, CancellationToken ct)
    {
        var assignment = await feeService.AssignFeeAsync(req);
        await SendSuccessAsync(MapAssignment(assignment), ct);
    }

    private static FeeAssignmentResponse MapAssignment(FeeAssignment a) => new(
        a.Id, a.FeeTemplateId, a.FeeTemplate?.Name ?? "",
        a.Scope.ToString(),
        a.FacultyId, a.Faculty?.Name,
        a.ProgramId, a.Program?.Name,
        a.StudentId, a.Student?.DisplayName ?? a.Student?.Email,
        a.SessionId, a.Session?.Name,
        a.AmountOverride, a.DueDateOverride, a.IsActive);
}

public sealed class GetAssignmentsEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<IEnumerable<FeeAssignmentResponse>>
{
    public override void Configure()
    {
        Get("/api/fees/assignments");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var templateIdStr = Query<string?>("templateId", isRequired: false);
        var sessionIdStr = Query<string?>("sessionId", isRequired: false);
        Guid? templateId = templateIdStr != null && Guid.TryParse(templateIdStr, out var t) ? t : null;
        Guid? sessionId = sessionIdStr != null && Guid.TryParse(sessionIdStr, out var s) ? s : null;
        var assignments = await feeService.GetAssignmentsAsync(templateId, sessionId);
        await SendSuccessAsync(assignments.Select(a => new FeeAssignmentResponse(
            a.Id, a.FeeTemplateId, a.FeeTemplate?.Name ?? "",
            a.Scope.ToString(),
            a.FacultyId, a.Faculty?.Name,
            a.ProgramId, a.Program?.Name,
            a.StudentId, a.Student?.DisplayName ?? a.Student?.Email,
            a.SessionId, a.Session?.Name,
            a.AmountOverride, a.DueDateOverride, a.IsActive)), ct);
    }
}

public sealed class DeleteAssignmentEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Delete("/api/fees/assignments/{id}");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            await feeService.DeleteAssignmentAsync(id);
            await SendSuccessAsync("Deleted", ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "Assignment not found", "NOT_FOUND", "Fee assignment not found", ct);
        }
    }
}
