using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Departments;

public sealed class DeleteDepartmentRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteDepartmentEndpoint(IDepartmentService departmentService)
    : ApiEndpoint<DeleteDepartmentRequest, object>
{
    public override void Configure()
    {
        Delete("/api/admin/departments/{id:guid}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Delete a department";
            s.Description = "Removes a department from the system.";
            s.Responses[200] = "Successfully deleted the department.";
            s.Responses[404] = "Department not found.";
        });
    }

    public override async Task HandleAsync(DeleteDepartmentRequest req, CancellationToken ct)
    {
        var result = await departmentService.DeleteAsync(req.Id, ct);
        await result.Match(
            _ => SendSuccessAsync(new { deleted = true }, ct, "Department deleted successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
