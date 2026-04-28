using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetGradebookSummaryEndpoint : ApiEndpointWithoutRequest<GradebookSummaryDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public GetGradebookSummaryEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Get("/api/gradebook/courses/{offeringId:guid}");
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
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        var result = await _gradebookService.GetGradebookSummaryAsync(offeringId, userId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
