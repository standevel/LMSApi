using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetProgramsRequest
{
    public Guid FacultyId { get; set; }
}

public sealed class GetProgramsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<GetProgramsRequest, IEnumerable<ProgramResponse>>
{
    public override void Configure()
    {
        Get("admissions/programs/{FacultyId}");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Get Programs By Faculty") 
            .WithTags("Admissions")
            .WithSummary("Retrieve all academic programs available under a specific faculty"));
    }

    public override async Task HandleAsync(GetProgramsRequest req, CancellationToken ct)
    {
        var programs = await admissionService.GetProgramsByFacultyAsync(req.FacultyId);
        var response = programs.Select(p => new ProgramResponse(p.Id, p.Name, p.Code));
        await SendSuccessAsync(response, ct);
    }
}
