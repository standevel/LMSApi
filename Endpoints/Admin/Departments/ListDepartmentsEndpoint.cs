using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Departments;

public sealed class ListDepartmentsEndpoint(IDepartmentService departmentService)
    : ApiEndpointWithoutRequest<List<DepartmentDto>>
{
    public override void Configure()
    {
        Get("/api/admin/departments");

        Summary(s =>
        {
            s.Summary = "List all departments";
            s.Description = "Retrieves a list of all registered departments.";
            s.Responses[200] = "Successfully retrieved the list of departments.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await departmentService.GetAllAsync(ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Departments retrieved successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
