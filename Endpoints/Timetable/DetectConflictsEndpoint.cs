using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class DetectConflictsRequest
{
    public Guid LecturerId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid? AcademicSessionId { get; set; }
    public Guid? VenueId { get; set; }
    public Guid? CourseOfferingId { get; set; }
}

public class DetectConflictsEndpoint(ITimetableService timetableService)
    : ApiEndpoint<DetectConflictsRequest, LMS.Api.Services.ConflictDetectionResult>
{
    public override void Configure()
    {
        Post("timetable/detect-conflicts");
        Roles("AcademicAdmin", "Admin", "Registrar", "SuperAdmin");
        Description(d => d
            .WithName("DetectConflicts")
            .WithTags("Timetable")
            .WithSummary("Endpoint for DetectConflicts"));
    }

    public override async Task HandleAsync(DetectConflictsRequest req, CancellationToken ct)
    {
        var result = await timetableService.DetectConflictsAsync(
            req.LecturerId, 
            req.DayOfWeek, 
            req.StartTime, 
            req.EndTime, 
            req.AcademicSessionId, 
            req.VenueId, 
            req.CourseOfferingId);
        await SendSuccessAsync(result, ct, "Conflict detection complete");
    }
}
