using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public sealed class UpdateSessionEndpoint : ApiEndpoint<UpdateSessionRequest, LectureSession>
{
    private readonly ISessionManagementService _sessionManagementService;

    public UpdateSessionEndpoint(ISessionManagementService sessionManagementService)
    {
        _sessionManagementService = sessionManagementService;
    }

    public override void Configure()
    {
        Put("/api/lecture-sessions/{id}");
        Roles("SuperAdmin", "Admin");
    }

    public override async Task HandleAsync(UpdateSessionRequest req, CancellationToken ct)
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
            var result = await _sessionManagementService.UpdateSessionAsync(sessionId, req, userId.Value);
            await SendSuccessAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Bad request", "VALIDATION_ERROR", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Internal server error", "INTERNAL_ERROR", ex.Message, ct);
        }
    }
}
