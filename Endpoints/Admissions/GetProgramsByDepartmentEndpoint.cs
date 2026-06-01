using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetProgramsByDepartmentRequest
{
    public Guid DepartmentId { get; set; }
}

public sealed class GetProgramsByDepartmentEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<GetProgramsByDepartmentRequest, IEnumerable<ProgramResponse>>
{
    public override void Configure()
    {
        Get("admissions/programs/department/{DepartmentId}");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Get Programs By Department") 
            .WithTags("Admissions")
            .WithSummary("Retrieve all academic programs offered by a specific department"));
    }

    public override async Task HandleAsync(GetProgramsByDepartmentRequest req, CancellationToken ct)
    {
        var programs = await admissionService.GetProgramsByDepartmentAsync(req.DepartmentId);
        var response = programs.Select(p => new ProgramResponse(p.Id, p.Name, p.Code));
        await SendSuccessAsync(response, ct);
    }
}
