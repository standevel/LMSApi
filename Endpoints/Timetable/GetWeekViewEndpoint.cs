using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public sealed record TimetableSlotDto(
    Guid Id,
    Guid CourseOfferingId,
    string? CourseTitle,
    string? CourseCode,
    Guid? LecturerId,
    string? LecturerName,
    string? LecturerEmail,
    List<string> CoLecturerIds,
    int DayOfWeek,
    string StartTime,
    string EndTime,
    int DurationMinutes,
    string? Notes
);

public class GetWeekViewEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<IEnumerable<TimetableSlotDto>>
{
    public override void Configure()
    {
        Get("/api/timetable/week-view/{sessionId}/{weekNumber}");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<Guid>("sessionId");
        var weekNumber = Route<int>("weekNumber");

        var lecturerIdRaw = Query<string?>("lecturerId", isRequired: false);
        Guid? lecturerId = null;
        if (!string.IsNullOrWhiteSpace(lecturerIdRaw) && Guid.TryParse(lecturerIdRaw, out var parsed))
            lecturerId = parsed;

        var slots = await timetableService.GetWeekViewAsync(sessionId, weekNumber, lecturerId);

        // Project to flat DTO immediately — avoids circular reference serialization issues
        var result = slots.Select(s => new TimetableSlotDto(
            s.Id,
            s.CourseOfferingId,
            s.CourseOffering?.Course?.Title,
            s.CourseOffering?.Course?.Code,
            s.LecturerId,
            s.Lecturer?.DisplayName,
            s.Lecturer?.Email,
            string.IsNullOrWhiteSpace(s.CoLecturersJson)
                ? []
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(s.CoLecturersJson) ?? [],
            (int)s.DayOfWeek,
            s.StartTime.ToString("HH:mm"),
            s.EndTime.ToString("HH:mm"),
            s.DurationMinutes,
            s.Notes
        )).ToList(); // Force evaluation before serialization

        await SendSuccessAsync(result, ct, "Week view timetable retrieved");
    }
}
