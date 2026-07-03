using FastEndpoints;
using LMS.Api.Services;
using LMS.Api.Security;

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
        Delete("admin/departments/{id:guid}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
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
