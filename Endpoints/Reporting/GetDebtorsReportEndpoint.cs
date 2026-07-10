using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.Extensions.Primitives;
using System;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetDebtorsReportEndpoint : ApiEndpointWithoutRequest<DebtorsReportResponseDto>
{
    private readonly IAnalyticsService _analyticsService;

    public GetDebtorsReportEndpoint(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public override void Configure()
    {
        Get("reports/financial/debtors");
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

        string? facultyIdStr = GetString(query, "facultyId");
        Guid? facultyId = Guid.TryParse(facultyIdStr, out var fid) ? fid : null;

        string? deptIdStr = GetString(query, "departmentId");
        Guid? deptId = Guid.TryParse(deptIdStr, out var did) ? did : null;

        string? progIdStr = GetString(query, "programId");
        Guid? progId = Guid.TryParse(progIdStr, out var pid) ? pid : null;

        string? levelIdStr = GetString(query, "levelId");
        Guid? levelId = Guid.TryParse(levelIdStr, out var lid) ? lid : null;

        var page = GetInt(query, "page") ?? 1;
        var pageSize = GetInt(query, "pageSize") ?? 25;
        bool exportAll = string.Equals(GetString(query, "exportAll"), "true", StringComparison.OrdinalIgnoreCase);

        var requestDto = new DebtorsReportRequestDto(
            sessionId,
            facultyId,
            deptId,
            progId,
            levelId,
            page,
            pageSize,
            exportAll
        );

        var result = await _analyticsService.GetDebtorsReportAsync(requestDto, ct);

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
