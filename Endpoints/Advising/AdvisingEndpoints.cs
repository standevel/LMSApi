using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Advising;

public sealed class GetEligibleAdvisersEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<AdviserUserDto>>
{
    public override void Configure()
    {
        Get("advising/eligible-advisers");
        Roles("SuperAdmin", "Admin", "HOD");
        Tags("Advising");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.GetEligibleAdvisersAsync(actorId.Value, QueryParam<Guid>("departmentId"), QueryParam<Guid>("facultyId"), ct), ct);
    }
}

public sealed class AssignAdviserEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<AssignAdviserRequest, AdviserAssignmentDto>
{
    public override void Configure()
    {
        Post("advising/assignments");
        Roles("SuperAdmin", "Admin", "HOD");
        Tags("Advising");
    }

    public override async Task HandleAsync(AssignAdviserRequest req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.AssignAdviserAsync(actorId.Value, req, ct), ct);
    }
}

public sealed class BulkAssignAdviserEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<BulkAssignAdviserRequest, List<AdviserAssignmentDto>>
{
    public override void Configure()
    {
        Post("advising/assignments/bulk");
        Roles("SuperAdmin", "Admin", "HOD");
        Tags("Advising");
    }

    public override async Task HandleAsync(BulkAssignAdviserRequest req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.BulkAssignAdviserAsync(actorId.Value, req, ct), ct);
    }
}

public sealed class AutoAssignAdvisersEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<AutoAssignAdvisersRequest, AutoAssignAdvisersResultDto>
{
    public override void Configure()
    {
        Post("advising/assignments/auto");
        Roles("SuperAdmin", "Admin", "HOD");
        Tags("Advising");
    }

    public override async Task HandleAsync(AutoAssignAdvisersRequest req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.AutoAssignAdvisersAsync(actorId.Value, req, ct), ct);
    }
}

public sealed class EndAdviserAssignmentEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<EndAdviserAssignmentEndpoint.Request, bool>
{
    public sealed class Request
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid Id { get; set; }
    }

    public override void Configure()
    {
        Delete("advising/assignments/{Id}");
        Roles("SuperAdmin", "Admin", "HOD");
        Tags("Advising");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await adviserService.EndAssignmentAsync(actorId.Value, req.Id, ct);
        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(true, ct);
    }
}

public sealed class GetAdviserDashboardEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<AdviserDashboardDto>
{
    public override void Configure()
    {
        Get("advising/dashboard");
        Roles("SuperAdmin", "Admin", "HOD", "Lecturer", "Adviser");
        Tags("Advising");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.GetDashboardAsync(actorId.Value, ct), ct);
    }
}

public sealed class GetAdviserStudentsEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<AdviserStudentSummaryDto>>
{
    public override void Configure()
    {
        Get("advising/students");
        Roles("SuperAdmin", "Admin", "HOD", "Lecturer", "Adviser");
        Tags("Advising");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.GetAssignedStudentsAsync(actorId.Value, ct), ct);
    }
}

public sealed class GetAdvisingStudentProfileEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetAdvisingStudentProfileEndpoint.Request, AdvisingStudentProfileDto>
{
    public sealed class Request
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
    }

    public override void Configure()
    {
        Get("advising/students/{StudentId}");
        Roles("SuperAdmin", "Admin", "HOD", "Lecturer", "Adviser");
        Tags("Advising");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.GetStudentProfileAsync(actorId.Value, req.StudentId, ct), ct);
    }
}

public sealed class GetAdvisingNotesEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<GetAdvisingNotesEndpoint.Request, List<AdvisingNoteDto>>
{
    public sealed class Request
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
    }

    public override void Configure()
    {
        Get("advising/students/{StudentId}/notes");
        Roles("SuperAdmin", "Admin", "HOD", "Lecturer", "Adviser");
        Tags("Advising");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.GetNotesAsync(actorId.Value, req.StudentId, ct), ct);
    }
}

public sealed class CreateAdvisingNoteEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateAdvisingNoteEndpoint.Request, AdvisingNoteDto>
{
    public sealed class Request
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime? FollowUpDateUtc { get; set; }
    }

    public override void Configure()
    {
        Post("advising/students/{StudentId}/notes");
        Roles("SuperAdmin", "Admin", "HOD", "Lecturer", "Adviser");
        Tags("Advising");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.CreateNoteAsync(actorId.Value, req.StudentId, new CreateAdvisingNoteRequest(req.Title, req.Body, req.FollowUpDateUtc), ct), ct);
    }
}

public sealed class VerifyRegistrationEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<VerifyRegistrationEndpoint.Request, RegistrationVerificationDto>
{
    public sealed class Request
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
        public string? Remarks { get; set; }
    }

    public override void Configure()
    {
        Post("advising/students/{StudentId}/registration/verify");
        Roles("SuperAdmin", "Admin", "Lecturer", "Adviser");
        Tags("Advising");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(await adviserService.VerifyRegistrationAsync(actorId.Value, req.StudentId, new VerifyRegistrationRequest(req.Remarks), ct), ct);
    }
}

public sealed class UnlockRegistrationEndpoint(IAdviserService adviserService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UnlockRegistrationEndpoint.Request, bool>
{
    public sealed class Request
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid StudentId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public override void Configure()
    {
        Post("advising/students/{StudentId}/registration/unlock");
        Roles("SuperAdmin", "Admin", "HOD");
        Tags("Advising");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var actorId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await adviserService.UnlockRegistrationAsync(actorId.Value, req.StudentId, new UnlockRegistrationVerificationRequest(req.Reason), ct);
        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(true, ct);
    }
}
