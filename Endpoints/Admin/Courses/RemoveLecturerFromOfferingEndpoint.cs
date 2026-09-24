using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class RemoveLecturerFromOfferingEndpoint(ICourseService courseService)
    : ApiEndpoint<RemoveOfferingLecturerRequest, CourseOfferingDto>
{
    public override void Configure()
    {
        Delete("admin/course-offerings/{id}/lecturers");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary     = "Remove a lecturer from a course offering";
            s.Description = "Removes a single lecturer (Main or Co-lecturer) from a course offering. "
                          + "Pass the LecturerId in the request body. "
                          + "Only the specified lecturer is removed; the remaining assignments are preserved. "
                          + "Use the assign-lecturer endpoint to replace the entire lecturer set.";
            s.Responses[200] = "Lecturer removed successfully.";
            s.Responses[404] = "The specified course offering or lecturer assignment was not found.";
        });
    }

    public override async Task HandleAsync(RemoveOfferingLecturerRequest req, CancellationToken ct)
    {
        var offeringId = Route<Guid>("id");
        var result = await courseService.RemoveLecturerAsync(offeringId, req.LecturerId, ct);
        await result.Match(
            data   => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct));
    }
}
