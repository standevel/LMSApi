using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class GetCourseOfferingTimetableEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<IEnumerable<LectureTimetableSlot>>
{
    public override void Configure()
    {
        Get("/api/timetable/course-offering/{CourseOfferingId}");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var courseOfferingId = Route<Guid>("CourseOfferingId");
        var result = await timetableService.GetCourseOfferingTimetableAsync(courseOfferingId);
        await SendSuccessAsync(result, ct, "Course offering timetable retrieved");
    }
}
