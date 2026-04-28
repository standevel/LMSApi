using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public class CreateManualSessionEndpoint(
    ILectureSessionService lectureSessionService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateManualSessionRequest, LectureSession>
{
    public override void Configure()
    {
        Post("/api/lecture-sessions/manual");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CreateManualSessionRequest req, CancellationToken ct)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            if (!userId.HasValue)
            {
                await SendFailureAsync(401, "User not authenticated", "UNAUTHORIZED", "User not authenticated", ct);
                return;
            }

            var result = await lectureSessionService.CreateManualSessionAsync(req, userId.Value);

            await SendSuccessAsync(result, ct, "Manual lecture session created successfully");
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_OPERATION", ex.Message, ct);
        }
    }
}
