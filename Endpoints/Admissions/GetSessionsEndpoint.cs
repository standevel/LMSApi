using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetSessionsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<EmptyRequest, IEnumerable<AcademicSessionResponse>>
{
    public override void Configure()
    {
        Get("/api/admissions/sessions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var sessions = await admissionService.GetAdmissionSessionsAsync();
        var response = sessions.Select(s => new AcademicSessionResponse(s.Id, s.Name, s.StartDate, s.EndDate, s.IsActive));
        await SendSuccessAsync(response, ct);
    }
}
