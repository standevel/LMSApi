using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetSystemGradingConfigurationEndpoint : ApiEndpointWithoutRequest<SystemGradingConfigurationDto>
{
    private readonly IGradebookService _gradebookService;

    public GetSystemGradingConfigurationEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("gradebook/system-configuration");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Check authentication and admin role
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();

        if (!userRoles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase) || r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Only administrators can view system configuration.", ct);
            return;
        }

        var result = await _gradebookService.GetSystemConfigurationAsync(ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
