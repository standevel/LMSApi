using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Departments;

public sealed class GetDepartmentByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetDepartmentByIdEndpoint(IDepartmentService departmentService)
    : ApiEndpoint<GetDepartmentByIdRequest, DepartmentDto>
{
    public override void Configure()
    {
        Get("/api/admin/departments/{id:guid}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Get department by ID";
            s.Description = "Retrieves the details of a specific department.";
            s.Responses[200] = "Successfully retrieved the department.";
            s.Responses[404] = "Department not found.";
        });
    }

    public override async Task HandleAsync(GetDepartmentByIdRequest req, CancellationToken ct)
    {
        var result = await departmentService.GetByIdAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Department retrieved successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
