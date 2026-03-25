using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetFacultiesEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<EmptyRequest, IEnumerable<FacultyResponse>>
{
    public override void Configure()
    {
        Get("/api/admissions/faculties");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var faculties = await admissionService.GetFacultiesAsync();
        var response = faculties.Select(f => new FacultyResponse(f.Id, f.Name, f.Label));
        await SendSuccessAsync(response, ct);
    }
}
