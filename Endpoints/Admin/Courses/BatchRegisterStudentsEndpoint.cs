using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class BatchRegisterStudentsEndpoint(ICourseService courseService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<BatchRegisterStudentsRequest, BatchRegistrationResultDto>
{
    public override void Configure()
    {
        Post("admin/courses/batch-register", "courses/batch-register");
        Group<AdminGroup>();
        Policies(LmsPolicies.CourseManagement);
        Tags("Administration", "Courses");
        Summary(s =>
        {
            s.Summary = "Batch-register students into a course offering";
            s.Description = "Enrolls multiple students into a specified course offering in batch. "
                          + "Automatically creates required user accounts if needed, reactivates dropped enrollments, "
                          + "and returns a breakdown of registered, already-registered, and failed students. "
                          + "Lecturers can register students for course offerings assigned to them.";
            s.Responses[200] = "Batch registration completed successfully.";
            s.Responses[400] = "Validation error (e.g. missing offering ID or no students specified).";
            s.Responses[403] = "Lecturer is not assigned to this course offering.";
            s.Responses[404] = "The specified course offering was not found.";
        });
    }

    public override async Task HandleAsync(BatchRegisterStudentsRequest req, CancellationToken ct)
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

        var result = await courseService.BatchRegisterStudentsAsync(req, userId.Value, bypassLecturerCheck, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
