using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetEnrollmentAnalyticsEndpoint : ApiEndpointWithoutRequest<EnrollmentAnalyticsDto>
{
    private readonly IAnalyticsService _analyticsService;

    public GetEnrollmentAnalyticsEndpoint(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

public override void Configure()
{
    Get("reports/enrollment-analytics");
    Tags("Reporting");
}

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        Guid.TryParse(sessionIdStr, out var sessionId);
        var result = await _analyticsService.GetEnrollmentAnalyticsAsync(sessionId == Guid.Empty ? null : sessionId, ct);

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
