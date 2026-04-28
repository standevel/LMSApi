using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class UpdateSystemGradingConfigurationEndpoint : ApiEndpoint<UpdateSystemGradingConfigurationRequest, SystemGradingConfigurationDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public UpdateSystemGradingConfigurationEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Put("/api/gradebook/system-configuration");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateSystemGradingConfigurationRequest req, CancellationToken ct)
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
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Only administrators can update system configuration.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var result = await _gradebookService.UpdateSystemConfigurationAsync(req, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Validation => 400,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "System configuration updated successfully");
    }
}
