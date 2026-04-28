using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class UploadMaterialRequest
{
    public IFormFile File { get; set; } = null!;
}

public sealed class UploadMaterialEndpoint : ApiEndpoint<UploadMaterialRequest, SessionMaterial>
{
    private readonly ISessionManagementService _sessionManagementService;

    public UploadMaterialEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Post("/api/lecture-sessions/{id}/materials");
        Roles("SuperAdmin", "Admin", "Lecturer");
        AllowFileUploads();
    }

    public override async Task HandleAsync(UploadMaterialRequest req, CancellationToken ct)
    {
        var sessionId = Route<Guid>("id");
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var file = req.File;
        if (file == null || file.Length == 0)
        {
            await SendFailureAsync(400, "Bad request", "NO_FILE", "No file was uploaded.", ct);
            return;
        }

        try
        {
            var result = await _sessionManagementService.UploadMaterialAsync(sessionId, file, userId.Value);
            await SendSuccessAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Bad request", "VALIDATION_ERROR", ex.Message, ct);
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
