using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicSessions;

public record ToggleAcademicSessionRegistrationStatusRequest(Guid Id);

public sealed class ToggleAcademicSessionRegistrationStatusEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<ToggleAcademicSessionRegistrationStatusRequest, AcademicSessionDto>
{
    public override void Configure()
    {
        Patch("admin/sessions/{id}/toggle-registration");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Toggle academic session course registration status";
            s.Description = "Opens or closes course registration for the specified academic session.";
            s.Response<ApiResponse<AcademicSessionDto>>(200, "Successfully toggled the registration status of the session.");
            s.Response<ApiResponse<object>>(404, "The specified session ID was not found.");
        });
    }

    public override async Task HandleAsync(ToggleAcademicSessionRegistrationStatusRequest req, CancellationToken ct)
    {
        var result = await sessionService.ToggleRegistrationOpenStatusAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
