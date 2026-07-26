using FastEndpoints;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class EmptyResponse { }

public class DeleteLectureTimetableSlotEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<EmptyResponse>
{
    public override void Configure()
    {
        Delete("timetable/slots/{SlotId}");
        Policies(PermissionPolicy.Build(LmsPermissions.TimetableManage));
        Description(d => d
            .WithName("DeleteLectureTimetableSlot")
            .WithTags("Timetable")
            .WithSummary("Delete a lecture timetable slot"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slotId = Route<Guid>("SlotId");
        await timetableService.DeleteLectureTimetableSlotAsync(slotId);
        await SendSuccessAsync(new EmptyResponse(), ct, "Timetable slot deleted successfully");
    }
}
