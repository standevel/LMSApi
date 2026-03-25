using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class GetCourseByIdEndpoint(ICourseService courseService)
    : ApiEndpoint<EmptyRequest, CourseDto>
{
    public override void Configure()
    {
        Get("/api/admin/courses/{id}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Get a course by ID";
            s.Description = "Retrieves the full details of a specific course, including its offerings and assigned lecturers, using its unique identifier.";
            s.Responses[200] = "Course details retrieved efficiently.";
            s.Responses[404] = "The specified course ID was not found in the system.";
        });
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await courseService.GetByIdAsync(id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
