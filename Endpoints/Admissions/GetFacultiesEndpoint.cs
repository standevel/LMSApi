using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetFacultiesEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<EmptyRequest, IEnumerable<FacultyResponse>>
{
    public override void Configure()
    {
        Get("admissions/faculties");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Get Faculties") 
            .WithTags("Admissions")
            .WithSummary("Retrieve all available faculties for admission"));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var faculties = await admissionService.GetFacultiesAsync();
        var response = faculties.Select(f => new FacultyResponse(f.Id, f.Name, f.Label));
        await SendSuccessAsync(response, ct);
    }
}
