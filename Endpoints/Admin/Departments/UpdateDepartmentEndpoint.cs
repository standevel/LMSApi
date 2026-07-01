using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Departments;

public sealed class UpdateDepartmentRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid FacultyId { get; set; }
    public Guid? HeadId { get; set; }
}

public sealed class UpdateDepartmentEndpoint(IDepartmentService departmentService)
    : ApiEndpoint<UpdateDepartmentRequest, DepartmentDto>
{
    public override void Configure()
    {
        Put("admin/departments/{id:guid}");
        Group<AdminGroup>();
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Update a department";
            s.Description = "Updates an existing department.";
            s.Responses[200] = "Successfully updated the department.";
            s.Responses[404] = "Department not found.";
        });
    }

    public override async Task HandleAsync(UpdateDepartmentRequest req, CancellationToken ct)
    {
        var request = new LMS.Api.Contracts.UpdateDepartmentRequest(
            req.Name,
            req.Code ?? "",
            req.FacultyId,
            req.HeadId);

        var result = await departmentService.UpdateAsync(req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct, "Department updated successfully"),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
