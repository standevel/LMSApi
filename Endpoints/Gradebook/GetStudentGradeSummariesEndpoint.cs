using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetStudentGradeSummariesEndpoint : ApiEndpointWithoutRequest<List<StudentGradeSummaryDto>>
{
    private readonly IGradebookService _gradebookService;

    public GetStudentGradeSummariesEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("/api/gradebook/courses/{offeringId:guid}/students");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");

        var result = await _gradebookService.GetStudentGradeSummariesAsync(offeringId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
