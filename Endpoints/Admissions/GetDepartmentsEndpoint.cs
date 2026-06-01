using FastEndpoints;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetDepartmentsRequest
{
    public Guid FacultyId { get; set; }
}

public sealed class GetDepartmentsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<GetDepartmentsRequest, IEnumerable<DepartmentDto>>
{
    public override void Configure()
    {
        Get("admissions/departments/faculty/{FacultyId}");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Get Departments") 
            .WithTags("Admissions")
            .WithSummary("Retrieve all departments under a specific faculty"));
    }

    public override async Task HandleAsync(GetDepartmentsRequest req, CancellationToken ct)
    {
        var departments = await admissionService.GetDepartmentsByFacultyAsync(req.FacultyId);
        var response = departments.Select(d => d.ToDto());
        await SendSuccessAsync(response, ct);
    }
}
