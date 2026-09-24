using FastEndpoints;
using LMS.Api.Data.Enums;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class CalculateDirectEntryPointsRequest
{
    public string Qualification { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
}

public sealed class CalculateDirectEntryPointsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<CalculateDirectEntryPointsRequest, DirectEntryPointsResult>
{
    public override void Configure()
    {
        Post("admissions/direct-entry/calculate-points");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Calculate Direct Entry Points")
            .WithTags("Admissions")
            .WithSummary("Calculate points for direct entry qualification and grade"));
    }

    public override async Task HandleAsync(CalculateDirectEntryPointsRequest req, CancellationToken ct)
    {
        DirectEntryQualification qualification = DirectEntryQualification.None;
        if (!string.IsNullOrEmpty(req.Qualification) && Enum.TryParse<DirectEntryQualification>(req.Qualification, out var parsedQual))
        {
            qualification = parsedQual;
        }

        DirectEntryGrade grade = DirectEntryGrade.Pass;
        if (!string.IsNullOrEmpty(req.Grade) && Enum.TryParse<DirectEntryGrade>(req.Grade, out var parsedGrade))
        {
            grade = parsedGrade;
        }

        var result = await admissionService.CalculateDirectEntryPointsAsync(qualification, grade, ct);
        await SendSuccessAsync(result, ct);
    }
}
