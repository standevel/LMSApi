using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Integration;

public sealed class TestExternalSystemConnectionEndpoint(IIntegrationService integrationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<TestExternalSystemConnectionRequest, bool>
{
    public override void Configure()
    {
        Post("integrations/test-connection");
        Roles("SuperAdmin", "Admin");
        Tags("Integration");
    }

    public override async Task HandleAsync(TestExternalSystemConnectionRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await integrationService.TestExternalSystemConnectionAsync(req.SystemType, ct);
        await SendAsync(result.Match(
            success => true,
            errors => false), ct);
    }
}

public sealed class GetAvailableIntegrationsEndpoint(IIntegrationService integrationService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<ExternalSystemInfoDto>>
{
    public override void Configure()
    {
        Get("integrations/available");
        Roles("SuperAdmin", "Admin", "Lecturer");
        Tags("Integration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await integrationService.GetAvailableIntegrationsAsync(ct);
        await SendAsync(result, ct);
    }
}