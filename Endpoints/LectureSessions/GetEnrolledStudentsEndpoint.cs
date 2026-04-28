using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class GetEnrolledStudentsEndpoint : ApiEndpointWithoutRequest<List<EnrolledStudent>>
{
    private readonly ISessionManagementService _sessionManagementService;

    public GetEnrolledStudentsEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Get("/api/lecture-sessions/{sessionId}/enrolled-students");
        Roles("SuperAdmin", "Admin", "Lecturer");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("sessionId");
        var userId = HttpContext.Items["CurrentUserId"] as Guid?;

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        try
        {
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");
            var result = await _sessionManagementService.GetEnrolledStudentsForSessionAsync(
                sessionId, userId.Value, isAdmin);
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
