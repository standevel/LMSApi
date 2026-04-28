using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class DetectConflictsRequest
{
    public Guid LecturerId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public class DetectConflictsEndpoint(ITimetableService timetableService)
    : ApiEndpoint<DetectConflictsRequest, ConflictDetectionResult>
{
    public override void Configure()
    {
        Post("/api/timetable/detect-conflicts");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(DetectConflictsRequest req, CancellationToken ct)
    {
        var result = await timetableService.DetectConflictsAsync(req.LecturerId, req.DayOfWeek, req.StartTime, req.EndTime);
        await SendSuccessAsync(result, ct, "Conflict detection complete");
    }
}
