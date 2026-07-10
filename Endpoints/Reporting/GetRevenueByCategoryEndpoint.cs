using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.Extensions.Primitives;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetRevenueByCategoryEndpoint : ApiEndpointWithoutRequest<List<RevenueByCategoryDto>>
{
    private readonly IAnalyticsService _analyticsService;

    public GetRevenueByCategoryEndpoint(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public override void Configure()
    {
        Get("reports/financial/revenue-by-category");
        Tags("Reporting - Financial");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var query = HttpContext.Request.Query;
        string? sessionIdStr = GetString(query, "sessionId") ?? GetString(query, "academicSessionId");
        Guid? sessionId = Guid.TryParse(sessionIdStr, out var sid) ? sid : null;

        var result = await _analyticsService.GetRevenueByCategoryAsync(sessionId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }

    private static string? GetString(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var values) || StringValues.IsNullOrEmpty(values))
            return null;
        var raw = values.ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }
}
