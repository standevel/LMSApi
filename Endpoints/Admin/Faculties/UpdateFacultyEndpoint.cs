using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Faculties;

public sealed class UpdateFacultyRequestWrapper
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class UpdateFacultyEndpoint(IFacultyService facultyService)
    : ApiEndpoint<UpdateFacultyRequestWrapper, FacultyDto>
{
    public override void Configure()
    {
        Put("/api/admin/faculties/{id}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Update a faculty";
            s.Description = "Updates the name or label of an existing university faculty.";
            s.Responses[200] = "Successfully updated the faculty.";
            s.Responses[404] = "Faculty not found.";
        });
    }

    public override async Task HandleAsync(UpdateFacultyRequestWrapper req, CancellationToken ct)
    {
        var request = new UpdateFacultyRequest(req.Name, req.Label);
        var result = await facultyService.UpdateAsync(req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Faculty updated successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
