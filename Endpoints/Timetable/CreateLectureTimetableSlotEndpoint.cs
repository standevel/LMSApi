using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class CreateLectureTimetableSlotRequest
{
    public Guid CourseOfferingId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid? LecturerId { get; set; }
    public List<Guid>? CoLecturerIds { get; set; }
    public Guid? VenueId { get; set; }
}

public class CreateLectureTimetableSlotEndpoint(ITimetableService timetableService)
    : ApiEndpoint<CreateLectureTimetableSlotRequest, object>
{
    public override void Configure()
    {
        Post("/api/timetable/slots");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CreateLectureTimetableSlotRequest req, CancellationToken ct)
    {
        try
        {
            var result = await timetableService.CreateLectureTimetableSlotAsync(
                req.CourseOfferingId, req.DayOfWeek, req.StartTime, req.EndTime,
                req.LecturerId, req.CoLecturerIds, req.VenueId);
            await SendSuccessAsync(result, ct, "Timetable slot created successfully");
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_OPERATION", ex.Message, ct);
        }
    }
}
