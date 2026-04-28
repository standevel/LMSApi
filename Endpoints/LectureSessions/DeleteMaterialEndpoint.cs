using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class DeleteMaterialEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly ISessionManagementService _sessionManagementService;

    public DeleteMaterialEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Delete("/api/lecture-sessions/materials/{materialId}");
        Roles("SuperAdmin", "Admin", "Lecturer");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var materialId = Route<Guid>("materialId");
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        try
        {
            await _sessionManagementService.DeleteMaterialAsync(materialId, userId.Value);
            await SendSuccessAsync(new { }, ct, "Material deleted successfully");
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(404, "Not found", "MATERIAL_NOT_FOUND", ex.Message, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            await SendFailureAsync(403, "Forbidden", "UNAUTHORIZED_ACCESS", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Internal server error", "INTERNAL_ERROR", ex.Message, ct);
        }
    }
}
