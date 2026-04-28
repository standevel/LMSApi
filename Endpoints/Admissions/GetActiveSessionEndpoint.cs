using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetActiveSessionEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<EmptyRequest, AcademicSessionResponse?>
{
    public override void Configure()
    {
        Get("/api/admissions/sessions/active");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var session = await admissionService.GetActiveAdmissionSessionAsync();
        
        if (session == null)
        {
            await SendFailureAsync(404, "No active academic session found", "not_found", "No active academic session found", ct);
            return;
        }

        var response = new AcademicSessionResponse(session.Id, session.Name, session.StartDate, session.EndDate, session.IsActive);
        await SendSuccessAsync(response, ct);
    }
}
