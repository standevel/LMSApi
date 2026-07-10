using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetTreasuryAnalyticsEndpoint : ApiEndpointWithoutRequest<TreasuryAnalyticsDto>
{
    private readonly IAnalyticsService _analyticsService;

    public GetTreasuryAnalyticsEndpoint(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public override void Configure()
    {
        Get("reports/treasury-analytics");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionIdString = Query<string>("academicSessionId", isRequired: false);
        Guid? sessionId = null;
        
        if (Guid.TryParse(sessionIdString, out var parsedSessionId))
        {
            sessionId = parsedSessionId;
        }

        var result = await _analyticsService.GetTreasuryAnalyticsAsync(sessionId == Guid.Empty ? null : sessionId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
