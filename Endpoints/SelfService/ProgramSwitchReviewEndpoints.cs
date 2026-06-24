using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.SelfService;

// ─────────────────────────────────────────────────────────────────────────────
// POST /self-service/program-switch/{id}/hod-review  (HoD only)
// Requires: status = PendingHoDReview  AND  JAMB doc uploaded
// ─────────────────────────────────────────────────────────────────────────────
public sealed class HoDReviewProgramSwitchEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpoint<ReviewProgramSwitchRequest, ProgramSwitchRequestDto>
{
    public override void Configure()
    {
        Post("self-service/program-switch/{id}/hod-review");
        Roles("HoD");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("HoDReviewProgramSwitch")
            .WithSummary(
                "Head of Department approves or rejects a program switch request. " +
                "Blocked if status ≠ PendingHoDReview or JAMB document is missing. " +
                "Approval advances status → PendingDeanReview."));
    }

    public override async Task HandleAsync(ReviewProgramSwitchRequest req, CancellationToken ct)
    {
        var reviewerId = await currentUser.GetUserIdAsync(ct);
        if (!reviewerId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var id = Route<Guid>("id");
        var result = await switchService.HoDReviewAsync(
            id, reviewerId.Value, req.Approved, req.Notes, req.RejectionReason, ct);

        if (result.IsError)
        {
            var first = result.FirstError;
            var code = first.Type == ErrorType.NotFound ? 404 : first.Type == ErrorType.Conflict ? 409 : 400;
            await SendFailureAsync(code, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /self-service/program-switch/{id}/dean-review  (Dean only)
// Requires: status = PendingDeanReview (HoD already approved)  AND  JAMB doc
// ─────────────────────────────────────────────────────────────────────────────
public sealed class DeanReviewProgramSwitchEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpoint<ReviewProgramSwitchRequest, ProgramSwitchRequestDto>
{
    public override void Configure()
    {
        Post("self-service/program-switch/{id}/dean-review");
        Roles("Dean");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("DeanReviewProgramSwitch")
            .WithSummary(
                "Dean approves or rejects a program switch request. " +
                "Blocked if HoD has not yet approved (status ≠ PendingDeanReview) or JAMB document is missing. " +
                "Approval advances status → PendingAdminAction."));
    }

    public override async Task HandleAsync(ReviewProgramSwitchRequest req, CancellationToken ct)
    {
        var reviewerId = await currentUser.GetUserIdAsync(ct);
        if (!reviewerId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var id = Route<Guid>("id");
        var result = await switchService.DeanReviewAsync(
            id, reviewerId.Value, req.Approved, req.Notes, req.RejectionReason, ct);

        if (result.IsError)
        {
            var first = result.FirstError;
            var code = first.Type == ErrorType.NotFound ? 404 : first.Type == ErrorType.Conflict ? 409 : 400;
            await SendFailureAsync(code, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /self-service/program-switch/{id}/complete  (Admin / Registrar)
// Requires: status = PendingAdminAction (HoD + Dean approved)  AND  JAMB doc
// Actually executes the program change in the database.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class AdminCompleteProgramSwitchEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpointWithoutRequest<ProgramSwitchRequestDto>
{
    public override void Configure()
    {
        Post("self-service/program-switch/{id}/complete");
        Roles("SuperAdmin", "Admin", "Registrar");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("AdminCompleteProgramSwitch")
            .WithSummary(
                "Admin/Registrar finalises the program switch. " +
                "Blocked if HoD or Dean have not yet approved, or JAMB document is missing. " +
                "Updates the student's program, enrollment, and triggers a new degree audit with historical grades."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var adminId = await currentUser.GetUserIdAsync(ct);
        if (!adminId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var id = Route<Guid>("id");
        var notes = Query<string>("notes", isRequired: false);

        var result = await switchService.AdminCompleteAsync(id, adminId.Value, notes, ct);
        if (result.IsError)
        {
            var first = result.FirstError;
            var code = first.Type == ErrorType.NotFound ? 404 : first.Type == ErrorType.Conflict ? 409 : 400;
            await SendFailureAsync(code, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /self-service/program-switch/{id}/reject  (Admin / Registrar)
// Rejects at the admin stage — requires JAMB doc + PendingAdminAction status.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class AdminRejectProgramSwitchEndpoint(
    IProgramSwitchService switchService,
    ICurrentUserContext currentUser)
    : ApiEndpoint<ReviewProgramSwitchRequest, ProgramSwitchRequestDto>
{
    public override void Configure()
    {
        Post("self-service/program-switch/{id}/reject");
        Roles("SuperAdmin", "Admin", "Registrar");
        Tags("ProgramSwitch");
        Description(d => d
            .WithName("AdminRejectProgramSwitch")
            .WithSummary("Admin/Registrar rejects a program switch request at the admin stage."));
    }

    public override async Task HandleAsync(ReviewProgramSwitchRequest req, CancellationToken ct)
    {
        var adminId = await currentUser.GetUserIdAsync(ct);
        if (!adminId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var id = Route<Guid>("id");
        var reason = req.RejectionReason ?? req.Notes ?? "Rejected by Admin.";
        var result = await switchService.AdminRejectAsync(id, adminId.Value, reason, ct);

        if (result.IsError)
        {
            var first = result.FirstError;
            var code = first.Type == ErrorType.NotFound ? 404 : first.Type == ErrorType.Conflict ? 409 : 400;
            await SendFailureAsync(code, first.Description, first.Code, first.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
