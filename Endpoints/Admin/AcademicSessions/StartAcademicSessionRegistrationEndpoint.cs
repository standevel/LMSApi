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

public class StartAcademicSessionRegistrationRequestWrapper
{
    public Guid Id { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool RunAutoRegistration { get; set; }
}

public sealed class StartAcademicSessionRegistrationEndpoint(IAcademicSessionService sessionService)
    : ApiEndpoint<StartAcademicSessionRegistrationRequestWrapper, AcademicSessionDto>
{
    public override void Configure()
    {
        Post("admin/sessions/{id}/start-registration");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Start course registration for an academic session";
            s.Description = "Opens course registration for students, optionally sets start and end dates, and optionally initiates batch auto-registration.";
            s.Response<ApiResponse<AcademicSessionDto>>(200, "Successfully opened course registration for the session.");
            s.Response<ApiResponse<object>>(404, "The specified session ID was not found.");
        });
    }

    public override async Task HandleAsync(StartAcademicSessionRegistrationRequestWrapper req, CancellationToken ct)
    {
        var request = new StartRegistrationRequest
        {
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            RunAutoRegistration = req.RunAutoRegistration
        };

        var result = await sessionService.StartRegistrationAsync(req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
