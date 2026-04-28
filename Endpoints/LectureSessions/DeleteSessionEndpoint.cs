using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class DeleteSessionEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly ISessionManagementService _sessionManagementService;

    public DeleteSessionEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Delete("/api/lecture-sessions/{id}");
        Roles("SuperAdmin", "Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("id");
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        try
        {
            await _sessionManagementService.DeleteSessionAsync(sessionId, userId.Value);
            await SendSuccessAsync(new { }, ct, "Session deleted successfully");
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(404, "Not found", "SESSION_NOT_FOUND", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Internal server error", "INTERNAL_ERROR", ex.Message, ct);
        }
    }
}
