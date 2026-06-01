using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public class GetAllCourseOfferingsForSessionEndpoint(ILectureSessionService lectureSessionService)
    : ApiEndpointWithoutRequest<List<CourseOfferingWithSlotCount>>
{
    public override void Configure()
    {
        Get("lecture-sessions/course-offerings/{academicSessionId}");
        Roles("Admin", "Registrar", "SuperAdmin");
        Description(d => d
            .WithName("GetAllCourseOfferingsForSession")
            .WithTags("Lecture Sessions")
            .WithSummary("Retrieve all course offerings for a specific academic session"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var academicSessionId = Route<Guid>("academicSessionId");
        var result = await lectureSessionService.GetCourseOfferingsWithTimetableSlotsAsync(academicSessionId);

        await SendSuccessAsync(result, ct, "Course offerings retrieved successfully");
    }
}
