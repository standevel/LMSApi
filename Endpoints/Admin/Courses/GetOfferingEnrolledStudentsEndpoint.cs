using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class GetOfferingEnrolledStudentsEndpoint(ICourseService courseService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<OfferingEnrolledStudentDto>>
{
    public override void Configure()
    {
        Get("admin/course-offerings/{offeringId:guid}/enrolled-students", "courses/{offeringId:guid}/enrolled-students");
        Group<AdminGroup>();
        Policies(LmsPolicies.CourseManagement);
        Tags("Administration", "Courses");
        Summary(s =>
        {
            s.Summary = "Get enrolled students for a course offering";
            s.Description = "Returns the list of students actively registered for the specified course offering, including matric numbers, names, and program/level details.";
            s.Responses[200] = "Successfully retrieved enrolled students.";
            s.Responses[403] = "Lecturer is not assigned to this course offering.";
            s.Responses[404] = "The specified course offering was not found.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var offeringId = Route<Guid>("offeringId");
        var userId = await currentUserContext.GetUserIdAsync(ct);

        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();

        var adminRoles = new[] { "Admin", "SuperAdmin", "ViceChancellor", "Dean", "HOD", "AcademicAdmin" };
        var bypassLecturerCheck = userRoles.Any(r => adminRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

        var result = await courseService.GetOfferingEnrolledStudentsAsync(offeringId, userId, bypassLecturerCheck, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
