using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class GetAvailableTimeSlotsEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<IEnumerable<object>>
{
    public override void Configure()
    {
        Get("timetable/available-slots/{LecturerId}/{DayOfWeek}");
        Roles("AcademicAdmin", "Admin", "Registrar", "SuperAdmin");
        Description(d => d
            .WithName("GetAvailableTimeSlots")
            .WithTags("Timetable")
            .WithSummary("Retrieve available time slots for a lecturer on a specific day"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var lecturerId = Route<Guid>("LecturerId");
        var dayOfWeek = Route<int>("DayOfWeek");

        var availableSlots = await timetableService.GetAvailableTimeSlotsAsync(lecturerId, dayOfWeek);

        var response = availableSlots.Select(s => new
        {
            start = s.Start.ToString("HH:mm"),
            end = s.End.ToString("HH:mm"),
            duration = (int)(s.End - s.Start).TotalMinutes
        }).ToList();

        await SendSuccessAsync(response, ct, "Available time slots retrieved");
    }
}
