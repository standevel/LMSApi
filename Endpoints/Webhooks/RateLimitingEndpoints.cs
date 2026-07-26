using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Webhooks; // I'll put this in Webhooks for now since it's related to API management

public sealed class GetRateLimitStatusEndpoint(IRateLimitingService rateLimitingService)
    : ApiEndpoint<GetRateLimitStatusEndpoint.GetRateLimitStatusRequest, ApiRateLimitDto>
{
    public class GetRateLimitStatusRequest
    {
        [Microsoft.AspNetCore.Mvc.FromQuery] public string ClientId { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromQuery] public string Endpoint { get; set; } = default!;
        [Microsoft.AspNetCore.Mvc.FromQuery] public string Method { get; set; } = default!;
    }

    public override void Configure()
    {
        Get("rate-limit/status");
        Policies(PermissionPolicy.Build(LmsPermissions.IntegrationsManage));
        Tags("RateLimiting");
    }

    public override async Task HandleAsync(GetRateLimitStatusRequest req, CancellationToken ct)
    {
        var result = await rateLimitingService.GetRateLimitStatusAsync(req.ClientId, req.Endpoint, req.Method, ct);
        await SendAsync(result, ct);
    }
}
