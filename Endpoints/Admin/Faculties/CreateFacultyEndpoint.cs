using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Faculties;

public sealed class CreateFacultyEndpoint(IFacultyService facultyService)
    : ApiEndpoint<CreateFacultyRequest, FacultyDto>
{
    public override void Configure()
    {
        Post("/api/admin/faculties");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Create a faculty";
            s.Description = "Registers a new university faculty or college.";
            s.Responses[200] = "Successfully created the faculty.";
        });
    }

    public override async Task HandleAsync(CreateFacultyRequest req, CancellationToken ct)
    {
        var result = await facultyService.CreateAsync(req, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Faculty created successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
