using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class GetAvailableTimeSlotsEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<IEnumerable<object>>
{
    public override void Configure()
    {
        Get("/api/timetable/available-slots/{LecturerId}/{DayOfWeek}");
        Roles("Admin", "Registrar", "SuperAdmin");
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
