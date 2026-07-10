using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class AssignLecturerToOfferingRequest
{
    public Guid? LecturerId { get; set; }
    public List<Guid>? CoLecturerIds { get; set; }
}

public sealed class AssignLecturerToOfferingEndpoint(ICourseService courseService)
    : ApiEndpoint<AssignLecturerToOfferingRequest, CourseOfferingDto>
{
    public override void Configure()
    {
        Patch("admin/course-offerings/{id}/assign-lecturer");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary     = "Assign lecturers to a course offering";
            s.Description = "Replaces all lecturer assignments for a course offering. "
                          + "Pass a LecturerId for the Main lecturer and optional CoLecturerIds. "
                          + "Pass null for LecturerId to unassign everyone.";
            s.Responses[200] = "Lecturer(s) successfully assigned.";
            s.Responses[404] = "The specified course offering or lecturer was not found.";
        });
    }

    public override async Task HandleAsync(AssignLecturerToOfferingRequest req, CancellationToken ct)
    {
        var offeringId = Route<Guid>("id");
        var result = await courseService.AssignLecturerAsync(offeringId, req.LecturerId, req.CoLecturerIds, ct);
        await result.Match(
            data   => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct));
    }
}
