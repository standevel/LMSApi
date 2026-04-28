using FastEndpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.LectureSessions;

public class GetTimetableSlotsForOfferingEndpoint(ILectureSessionService lectureSessionService)
    : ApiEndpointWithoutRequest<List<LectureTimetableSlot>>
{
    public override void Configure()
    {
        Get("/api/lecture-sessions/timetable-slots/{offeringId}");
        Roles("Admin", "Registrar", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var offeringId = Route<Guid>("offeringId");
        var result = await lectureSessionService.GetTimetableSlotsForOfferingAsync(offeringId);

        await SendSuccessAsync(result, ct, "Timetable slots retrieved successfully");
    }
}
