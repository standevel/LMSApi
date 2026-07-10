using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public record ToggleAcademicSessionAdmissionOpenStatusRequest(Guid Id);

public sealed class ToggleAcademicSessionAdmissionOpenStatusEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<ToggleAcademicSessionAdmissionOpenStatusRequest, AcademicSessionDto>
{
    public override void Configure()
    {
        Patch("admin/sessions/{id}/toggle-admission-open");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Toggle academic session admission portal open status";
            s.Description = "Opens or closes the admission portal for the specified academic session.";
            s.Response<ApiResponse<AcademicSessionDto>>(200, "Successfully toggled the admission portal open status of the session.");
            s.Response<ApiResponse<object>>(404, "The specified session ID was not found.");
        });
    }

    public override async Task HandleAsync(ToggleAcademicSessionAdmissionOpenStatusRequest req, CancellationToken ct)
    {
        var result = await sessionService.ToggleAdmissionOpenStatusAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
