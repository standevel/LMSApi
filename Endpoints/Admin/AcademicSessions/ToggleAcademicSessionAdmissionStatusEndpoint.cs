using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public record ToggleAcademicSessionAdmissionStatusRequest(Guid Id);

public sealed class ToggleAcademicSessionAdmissionStatusEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<ToggleAcademicSessionAdmissionStatusRequest, AcademicSessionDto>
{
    public override void Configure()
    {
        Patch("admin/sessions/{id}/toggle-admission-status");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Toggle academic session admission active status";
            s.Description = "Activates or deactivates an academic session specifically for admissions. Active admission sessions are targeted for student application and admission processing.";
            s.Response<ApiResponse<AcademicSessionDto>>(200, "Successfully toggled the active admission status of the session.");
            s.Response<ApiResponse<object>>(404, "The specified session ID was not found.");
        });
    }

    public override async Task HandleAsync(ToggleAcademicSessionAdmissionStatusRequest req, CancellationToken ct)
    {
        var result = await sessionService.ToggleAdmissionStatusAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
