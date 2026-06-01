using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetAcademicLevelsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<EmptyRequest, IEnumerable<AcademicLevelResponse>>
{
    public override void Configure()
    {
        Get("admissions/levels");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Get Academic Levels") 
            .WithTags("Admissions")
            .WithSummary("Retrieve all academic levels (e.g., 100, 200, 300) available for admission"));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var levels = await admissionService.GetAcademicLevelsAsync();
        var response = levels.Select(l => new AcademicLevelResponse(
            l.Id,
            l.Name,
            l.Order,
            l.ProgramId,
            l.Program?.Name ?? string.Empty
        ));
        await SendSuccessAsync(response, ct);
    }
}
