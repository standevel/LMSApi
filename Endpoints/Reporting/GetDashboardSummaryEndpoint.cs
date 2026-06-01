using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetDashboardSummaryEndpoint : ApiEndpointWithoutRequest<DashboardSummaryDto>
{
    private readonly IAnalyticsService _analyticsService;

    public GetDashboardSummaryEndpoint(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public override void Configure()
    {
        Get("api/reports/dashboard/summary");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var result = await _analyticsService.GetDashboardSummaryAsync(ct);

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
