using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Faculties;

public sealed class ListFacultiesEndpoint(IFacultyService facultyService)
    : ApiEndpointWithoutRequest<List<FacultyDto>>
{
    public override void Configure()
    {
        Get("/api/admin/faculties");

        Summary(s =>
        {
            s.Summary = "List all faculties";
            s.Description = "Retrieves a list of all registered university faculties and colleges.";
            s.Responses[200] = "Successfully retrieved the list of faculties.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await facultyService.GetAllAsync(ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Faculties retrieved successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
