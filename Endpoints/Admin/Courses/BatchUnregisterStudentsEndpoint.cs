using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class BatchUnregisterStudentsEndpoint(ICourseService courseService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<BatchUnregisterStudentsRequest, BatchUnregisterResultDto>
{
    public override void Configure()
    {
        Post("admin/courses/batch-unregister", "courses/batch-unregister");
        Group<AdminGroup>();
        Policies(LmsPolicies.CourseManagement);
        Tags("Administration", "Courses");
        Summary(s =>
        {
            s.Summary = "Batch-unregister/drop students from a course offering";
            s.Description = "Unenrolls/drops multiple students from a specified course offering in batch. "
                          + "Marks the enrollment status as Dropped, updates the drop timestamp, "
                          + "and returns a breakdown of dropped, already-dropped/not-enrolled, and failed students. "
                          + "Lecturers can unregister students for course offerings assigned to them.";
            s.Responses[200] = "Batch unregistration completed successfully.";
            s.Responses[400] = "Validation error (e.g. missing offering ID or no students specified).";
            s.Responses[403] = "Lecturer is not assigned to this course offering.";
            s.Responses[404] = "The specified course offering was not found.";
            s.Responses[409] = "Grades have already been published for this course offering.";
        });
    }

    public override async Task HandleAsync(BatchUnregisterStudentsRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();

        var adminRoles = new[] { "Admin", "SuperAdmin", "ViceChancellor", "Dean", "HOD", "AcademicAdmin" };
        var bypassLecturerCheck = userRoles.Any(r => adminRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

        var result = await courseService.BatchUnregisterStudentsAsync(req, userId.Value, bypassLecturerCheck, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
