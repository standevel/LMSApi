using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class EmptyResponse { }

public class DeleteLectureTimetableSlotEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<EmptyResponse>
{
    public override void Configure()
    {
        Delete("/api/timetable/slots/{SlotId}");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slotId = Route<Guid>("SlotId");
        await timetableService.DeleteLectureTimetableSlotAsync(slotId);
        await SendSuccessAsync(new EmptyResponse(), ct, "Timetable slot deleted successfully");
    }
}
