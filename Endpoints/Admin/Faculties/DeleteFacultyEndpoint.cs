using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Faculties;

public sealed class DeleteFacultyRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteFacultyEndpoint(IFacultyService facultyService)
    : ApiEndpoint<DeleteFacultyRequest, string>
{
    public override void Configure()
    {
        Delete("/api/admin/faculties/{id:guid}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Delete a faculty";
            s.Description = "Removes a university faculty from the system.";
            s.Responses[204] = "Successfully deleted the faculty.";
            s.Responses[404] = "Faculty not found.";
        });
    }

    public override async Task HandleAsync(DeleteFacultyRequest req, CancellationToken ct)
    {
        var result = await facultyService.DeleteAsync(req.Id, ct);
        await result.Match(
            _ => SendSuccessAsync("Deleted", ct, "Faculty deleted successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
