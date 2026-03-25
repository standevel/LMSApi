using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace LMS.Api.Endpoints.Admin.Curricula;

public sealed class GetCurriculumHistoryEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<GetCurriculumHistoryRequest, List<CurriculumHistoryDto>>
{
    public override void Configure()
    {
        Get("/api/admin/curricula/{CurriculumId}/history");
        Summary(s =>
        {
            s.Summary = "Get curriculum update history";
            s.Description = "Retrieves a list of audit logs associated with a specific curriculum.";
            s.Responses[200] = "Successfully retrieved the curriculum history.";
            s.Responses[404] = "The specified curriculum was not found.";
        });
    }

    public override async Task HandleAsync(GetCurriculumHistoryRequest req, CancellationToken ct)
    {
        var result = await curriculumService.GetHistoryAsync(req.CurriculumId, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class GetCurriculumHistoryRequest
{
    public Guid CurriculumId { get; set; }
}
