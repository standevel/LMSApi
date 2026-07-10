using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class BulkAssignLecturersEndpoint(ICourseService courseService)
    : ApiEndpoint<BulkAssignLecturersRequest, BulkAssignLecturersResult>
{
    public override void Configure()
    {
        Patch("admin/course-offerings/assignments/bulk");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Bulk-assign lecturers to course offerings";
            s.Description = "Assigns (or clears) a lecturer on each specified course offering independently. "
                          + "Useful when one course spans multiple programs and each program needs its own lecturer. "
                          + "Pass null for LecturerId to unassign.";
            s.Responses[200] = "Assignments applied. Check the Errors list for any partial failures.";
            s.Responses[400] = "No assignments provided.";
        });
    }

    public override async Task HandleAsync(BulkAssignLecturersRequest req, CancellationToken ct)
    {
        var result = await courseService.BulkAssignLecturersAsync(req.Assignments, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
