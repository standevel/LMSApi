using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetResultUploadAggregatesEndpoint : ApiEndpoint<ResultUploadAggregatesRequest, ResultUploadAggregatesResponse>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public GetResultUploadAggregatesEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Get("gradebook/result-upload-aggregates");
        Policies(LmsPolicies.AcademicManagement);
        Tags("Gradebook");
        Summary(s =>
        {
            s.Summary = "Get aggregates of result uploads";
            s.Description = "Returns aggregates and detailed progress of course result uploads per selected or active semester, summarized by colleges, programs, and course offerings.";
            s.Responses[200] = "Aggregates retrieved successfully.";
            s.Responses[400] = "Bad request.";
            s.Responses[401] = "Unauthorized.";
            s.Responses[403] = "Forbidden.";
        });
    }

    public override async Task HandleAsync(ResultUploadAggregatesRequest req, CancellationToken ct)
    {
        var userId = await _currentUserContext.GetUserIdAsync(ct) ?? Guid.Empty;

        var result = await _gradebookService.GetResultUploadAggregatesAsync(req, userId, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
