using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public class ValidateSessionConflictsEndpoint(ILectureSessionService lectureSessionService)
    : ApiEndpoint<ValidateConflictsRequest, List<ConflictWarning>>
{
    public override void Configure()
    {
        Post("lecture-sessions/validate-conflicts");
        Roles("Admin", "Registrar", "SuperAdmin");
        Description(d => d
            .WithName("ValidateSessionConflicts")
            .WithTags("Lecture Sessions")
            .WithSummary("Validate session conflicts"));
    }

    public override async Task HandleAsync(ValidateConflictsRequest req, CancellationToken ct)
    {
        var result = await lectureSessionService.DetectConflictsAsync(
            req.Date,
            req.StartTime,
            req.EndTime,
            req.LecturerIds,
            req.VenueId);

        await SendSuccessAsync(result, ct, "Conflict validation completed");
    }
}
