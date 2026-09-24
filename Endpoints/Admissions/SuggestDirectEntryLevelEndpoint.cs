using FastEndpoints;
using LMS.Api.Data.Enums;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class SuggestDirectEntryLevelRequest
{
    public string Qualification { get; set; } = string.Empty;
}

public sealed class SuggestDirectEntryLevelEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<SuggestDirectEntryLevelRequest, LevelSuggestionResult>
{
    public override void Configure()
    {
        Post("admissions/direct-entry/suggest-level");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Suggest Direct Entry Level")
            .WithTags("Admissions")
            .WithSummary("Suggest starting level based on direct entry qualification"));
    }

    public override async Task HandleAsync(SuggestDirectEntryLevelRequest req, CancellationToken ct)
    {
        DirectEntryQualification qualification = DirectEntryQualification.None;
        if (!string.IsNullOrEmpty(req.Qualification) && Enum.TryParse<DirectEntryQualification>(req.Qualification, out var parsed))
        {
            qualification = parsed;
        }

        var result = await admissionService.SuggestStartingLevelForQualificationAsync(qualification, ct);
        await SendSuccessAsync(result, ct);
    }
}
