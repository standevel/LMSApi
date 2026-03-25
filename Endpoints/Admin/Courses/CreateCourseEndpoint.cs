using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class CreateCourseEndpoint(ICourseService courseService)
    : ApiEndpoint<CreateCourseRequest, CourseDto>
{
    public override void Configure()
    {
        Post("/api/admin/courses");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Create a new course";
            s.Description = "Creates a new academic course along with its initial offerings. The course code must be unique.";
            s.Response<ApiResponse<CourseDto>>(200, "Course created successfully.");
            s.Response<ApiResponse<object>>(400, "Validation error or duplicate course code.");
        });
    }

    public override async Task HandleAsync(CreateCourseRequest req, CancellationToken ct)
    {
        var result = await courseService.CreateAsync(req, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
