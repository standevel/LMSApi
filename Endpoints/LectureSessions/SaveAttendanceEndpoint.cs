using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class SaveAttendanceEndpoint : ApiEndpoint<SaveAttendanceRequest, AttendanceStatistics>
{
    private readonly ISessionManagementService _sessionManagementService;

    public SaveAttendanceEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Post("/api/lecture-sessions/{id}/attendance");
        Roles("SuperAdmin", "Admin", "Lecturer");
    }

    public override async Task HandleAsync(SaveAttendanceRequest req, CancellationToken ct)
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
            var result = await _sessionManagementService.SaveAttendanceAsync(sessionId, req.Records, userId.Value);
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
