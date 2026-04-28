using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class GetSessionsEndpoint : ApiEndpoint<SessionFilterRequest, PagedResult<SessionListItem>>
{
    private readonly ISessionManagementService _sessionManagementService;

    public GetSessionsEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Get("/api/lecture-sessions");
        Roles("SuperAdmin", "Admin", "Lecturer");
    }

    public override async Task HandleAsync(SessionFilterRequest req, CancellationToken ct)
    {
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        try
        {
            var result = await _sessionManagementService.GetSessionsAsync(req, userId.Value, isAdmin);
            await SendSuccessAsync(result, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Internal server error", "INTERNAL_ERROR", ex.Message, ct);
        }
    }
}
