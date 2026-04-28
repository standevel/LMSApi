using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class GetSessionDetailsEndpoint : ApiEndpointWithoutRequest<SessionDetailsResponse>
{
    private readonly ISessionManagementService _sessionManagementService;

    public GetSessionDetailsEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Get("/api/lecture-sessions/{id}");
        Roles("SuperAdmin", "Admin", "Lecturer");
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

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        try
        {
            var result = await _sessionManagementService.GetSessionDetailsAsync(sessionId, userId.Value, isAdmin);
            await SendSuccessAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(404, "Not found", "SESSION_NOT_FOUND", ex.Message, ct);
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
