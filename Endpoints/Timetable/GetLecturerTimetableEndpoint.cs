using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class GetLecturerTimetableEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<IEnumerable<LectureTimetableSlot>>
{
    public override void Configure()
    {
        Get("timetable/lecturer/{LecturerId}");
        Roles("AcademicAdmin", "Admin", "Registrar", "SuperAdmin", "Lecturer");
        Description(d => d
            .WithName("GetLecturerTimetable")
            .WithTags("Timetable")
            .WithSummary("Retrieve timetable for a specific lecturer"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var lecturerId = Route<Guid>("LecturerId");
        var result = await timetableService.GetLecturerTimetableAsync(lecturerId);
        await SendSuccessAsync(result, ct, "Lecturer timetable retrieved");
    }
}
