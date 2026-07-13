using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.Extensions.Primitives;

namespace LMS.Api.Endpoints.Reporting;

public sealed class SendFeeRemindersEndpoint : ApiEndpointWithoutRequest<FeeReminderResult>
{
    private readonly IAnalyticsService _analyticsService;

    public SendFeeRemindersEndpoint(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public override void Configure()
    {
        Post("reports/financial/send-fee-reminders");
        Tags("Reporting - Financial");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = HttpContext.Request.Query;
        string? sessionIdStr = GetString(query, "sessionId") ?? GetString(query, "academicSessionId");
        Guid? sessionId = Guid.TryParse(sessionIdStr, out var sid) ? sid : null;

        var result = await _analyticsService.SendFeeRemindersAsync(sessionId, ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
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
