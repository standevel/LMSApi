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

public record EndAcademicSessionRegistrationRequest(Guid Id);

public sealed class EndAcademicSessionRegistrationEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<EndAcademicSessionRegistrationRequest, AcademicSessionDto>
{
    public override void Configure()
    {
        Post("admin/sessions/{id}/end-registration");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "End course registration for an academic session";
            s.Description = "Closes course registration immediately for students, setting the registration status to closed.";
            s.Response<ApiResponse<AcademicSessionDto>>(200, "Successfully closed course registration for the session.");
            s.Response<ApiResponse<object>>(404, "The specified session ID was not found.");
        });
    }

    public override async Task HandleAsync(EndAcademicSessionRegistrationRequest req, CancellationToken ct)
    {
        var result = await sessionService.EndRegistrationAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
