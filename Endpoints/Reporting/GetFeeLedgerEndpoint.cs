using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.Extensions.Primitives;
using System;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetFeeLedgerEndpoint : ApiEndpointWithoutRequest<FeeLedgerResponseDto>
{
    private readonly IAnalyticsService _analyticsService;

    public GetFeeLedgerEndpoint(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public override void Configure()
    {
        Get("reports/financial/fee-ledger");
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

        // Extract parameters
        string? sessionIdStr = GetString(query, "sessionId") ?? GetString(query, "academicSessionId");
        Guid? sessionId = Guid.TryParse(sessionIdStr, out var sid) ? sid : null;

        string? startDateStr = GetString(query, "startDate");
        DateTime? startDate = DateTime.TryParse(startDateStr, out var sd) ? sd : null;

        string? endDateStr = GetString(query, "endDate");
        DateTime? endDate = DateTime.TryParse(endDateStr, out var ed) ? ed : null;

        string? paymentMethodStr = GetString(query, "paymentMethod");
        int? paymentMethod = int.TryParse(paymentMethodStr, out var pm) ? pm : null;

        string? searchTerm = GetString(query, "search");
        var page = GetInt(query, "page") ?? 1;
        var pageSize = GetInt(query, "pageSize") ?? 25;
        bool exportAll = string.Equals(GetString(query, "exportAll"), "true", StringComparison.OrdinalIgnoreCase);

        var requestDto = new FeeLedgerRequestDto(
            sessionId,
            startDate,
            endDate,
            paymentMethod,
            searchTerm,
            page,
            pageSize,
            exportAll
        );

        var result = await _analyticsService.GetFeeLedgerAsync(requestDto, ct);

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

    private static int? GetInt(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var values) || StringValues.IsNullOrEmpty(values))
            return null;
        var raw = values.ToString();
        return int.TryParse(raw, out var val) ? val : null;
    }
}
