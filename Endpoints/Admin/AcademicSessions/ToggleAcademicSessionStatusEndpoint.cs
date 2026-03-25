using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public record ToggleAcademicSessionStatusRequest(Guid Id);

public sealed class ToggleAcademicSessionStatusEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<ToggleAcademicSessionStatusRequest, AcademicSessionDto>
{
    public override void Configure()
    {
        Patch("/api/admin/sessions/{id}/toggle-status");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Toggle academic session status";
            s.Description = "Activates or deactivates an academic session. Active sessions are targeted for course enrollment and grading.";
            s.Response<ApiResponse<AcademicSessionDto>>(200, "Successfully toggled the active status of the session.");
            s.Response<ApiResponse<object>>(404, "The specified session ID was not found.");
        });
    }

    public override async Task HandleAsync(ToggleAcademicSessionStatusRequest req, CancellationToken ct)
    {
        var result = await sessionService.ToggleStatusAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
