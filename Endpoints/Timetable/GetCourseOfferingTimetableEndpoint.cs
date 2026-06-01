using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Timetable;

public class GetCourseOfferingTimetableEndpoint(ITimetableService timetableService)
    : ApiEndpointWithoutRequest<IEnumerable<LectureTimetableSlot>>
{
    public override void Configure()
    {
        Get("timetable/course-offering/{CourseOfferingId}");
        Roles("Admin", "Registrar", "SuperAdmin");
        Description(d => d
            .WithName("GetCourseOfferingTimetable")
            .WithTags("Timetable")
            .WithSummary("Retrieve timetable for a specific course offering"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var courseOfferingId = Route<Guid>("CourseOfferingId");
        var result = await timetableService.GetCourseOfferingTimetableAsync(courseOfferingId);
        await SendSuccessAsync(result, ct, "Course offering timetable retrieved");
    }
}
