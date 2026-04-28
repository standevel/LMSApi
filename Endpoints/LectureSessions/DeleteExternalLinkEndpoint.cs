using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class DeleteExternalLinkEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly ISessionManagementService _sessionManagementService;

    public DeleteExternalLinkEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Delete("/api/lecture-sessions/external-links/{id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var linkId = Route<Guid>("id");
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        try
        {
            await _sessionManagementService.DeleteExternalLinkAsync(linkId, userId.Value);
            await SendSuccessAsync(new { message = "External link deleted successfully" }, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(404, "Not found", "NOT_FOUND", ex.Message, ct);
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
