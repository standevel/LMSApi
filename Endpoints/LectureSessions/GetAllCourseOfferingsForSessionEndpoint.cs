using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public class GetAllCourseOfferingsForSessionEndpoint(ILectureSessionService lectureSessionService)
    : ApiEndpointWithoutRequest<List<CourseOfferingWithSlotCount>>
{
    public override void Configure()
    {
        Get("/api/lecture-sessions/course-offerings/{academicSessionId}");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var academicSessionId = Route<Guid>("academicSessionId");
        var result = await lectureSessionService.GetCourseOfferingsWithTimetableSlotsAsync(academicSessionId);

        await SendSuccessAsync(result, ct, "Course offerings retrieved successfully");
    }
}
