using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public class GenerateBulkSessionsForSemesterEndpoint(
    ILectureSessionService lectureSessionService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<GenerateBulkSessionsRequest, BulkSessionGenerationResult>
{
    public override void Configure()
    {
        Post("/api/lecture-sessions/generate-bulk");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(GenerateBulkSessionsRequest req, CancellationToken ct)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            if (!userId.HasValue)
            {
                await SendFailureAsync(401, "User not authenticated", "UNAUTHORIZED", "User not authenticated", ct);
                return;
            }

            var result = await lectureSessionService.GenerateBulkSessionsForSemesterAsync(
                req.AcademicSessionId,
                req.EndDate,
                userId.Value);

            await SendSuccessAsync(result, ct, "Bulk lecture sessions generated successfully");
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_OPERATION", ex.Message, ct);
        }
    }
}
