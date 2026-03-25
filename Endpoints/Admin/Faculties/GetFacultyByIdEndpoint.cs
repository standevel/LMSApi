using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Faculties;

public sealed class GetFacultyByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetFacultyByIdEndpoint(IFacultyService facultyService)
    : ApiEndpoint<GetFacultyByIdRequest, FacultyDto>
{
    public override void Configure()
    {
        Get("/api/admin/faculties/{id:guid}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Get faculty by ID";
            s.Description = "Retrieves the details of a specific university faculty.";
            s.Responses[200] = "Successfully retrieved the faculty.";
            s.Responses[404] = "Faculty not found.";
        });
    }

    public override async Task HandleAsync(GetFacultyByIdRequest req, CancellationToken ct)
    {
        var result = await facultyService.GetByIdAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Faculty retrieved successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
