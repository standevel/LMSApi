using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class ListCoursesEndpoint(ICourseService courseService)
    : ApiEndpoint<EmptyRequest, List<CourseDto>>
{
    public override void Configure()
    {
        Get("/api/admin/courses");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "List all courses";
            s.Description = "Retrieves a comprehensive list of all courses, including their associated offerings, programs, levels, and lecturers.";
            s.Response<ApiResponse<List<CourseDto>>>(200, "Successfully retrieved the list of courses.");
        });
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await courseService.GetAllAsync(ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
