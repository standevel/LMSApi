using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class UpdateLectureTimetableSlotRequest
{
    public Guid? NewLecturerId { get; set; }
    public List<Guid>? CoLecturerIds { get; set; }
    public TimeOnly? NewStartTime { get; set; }
    public TimeOnly? NewEndTime { get; set; }
    public Guid? NewVenueId { get; set; }
}

public class UpdateLectureTimetableSlotEndpoint(ITimetableService timetableService)
    : ApiEndpoint<UpdateLectureTimetableSlotRequest, object>
{
    public override void Configure()
    {
        Put("/api/timetable/slots/{SlotId}");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(UpdateLectureTimetableSlotRequest req, CancellationToken ct)
    {
        var slotId = Route<Guid>("SlotId");
        var result = await timetableService.UpdateLectureTimetableSlotAsync(
            slotId, req.NewLecturerId, req.CoLecturerIds, req.NewStartTime, req.NewEndTime, req.NewVenueId);
        await SendSuccessAsync(result, ct, "Timetable slot updated successfully");
    }
}
