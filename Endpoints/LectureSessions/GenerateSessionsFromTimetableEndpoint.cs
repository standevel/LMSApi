using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public class GenerateSessionsFromTimetableEndpoint(
    ILectureSessionService lectureSessionService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<GenerateSessionsRequest, SessionGenerationResult>
{
    public override void Configure()
    {
        Post("/api/lecture-sessions/generate");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(GenerateSessionsRequest req, CancellationToken ct)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            if (!userId.HasValue)
            {
                await SendFailureAsync(401, "User not authenticated", "UNAUTHORIZED", "User not authenticated", ct);
                return;
            }

            var result = await lectureSessionService.GenerateSessionsFromTimetableAsync(
                req.TimetableSlotIds,
                req.EndDate,
                userId.Value);

            await SendSuccessAsync(result, ct, "Lecture sessions generated successfully");
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_OPERATION", ex.Message, ct);
        }
    }
}
