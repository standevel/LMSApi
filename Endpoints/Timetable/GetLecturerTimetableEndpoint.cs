using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class GetLecturerTimetableEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<IEnumerable<LectureTimetableSlot>>
{
    public override void Configure()
    {
        Get("/api/timetable/lecturer/{LecturerId}");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var lecturerId = Route<Guid>("LecturerId");
        var result = await timetableService.GetLecturerTimetableAsync(lecturerId);
        await SendSuccessAsync(result, ct, "Lecturer timetable retrieved");
    }
}
