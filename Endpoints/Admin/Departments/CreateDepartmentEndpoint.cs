using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Departments;

public sealed class CreateDepartmentEndpoint(IDepartmentService departmentService)
    : ApiEndpoint<CreateDepartmentRequest, DepartmentDto>
{
    public override void Configure()
    {
        Post("admin/departments");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Create a department";
            s.Description = "Registers a new department within a faculty.";
            s.Responses[200] = "Successfully created the department.";
        });
    }

    public override async Task HandleAsync(CreateDepartmentRequest req, CancellationToken ct)
    {
        var result = await departmentService.CreateAsync(req, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Department created successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
