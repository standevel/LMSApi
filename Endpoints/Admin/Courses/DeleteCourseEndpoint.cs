using ErrorOr;
using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Contracts;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class DeleteCourseEndpoint(ICourseService courseService)
    : ApiEndpoint<EmptyRequest, bool>
{
    public override void Configure()
    {
        Delete("/api/admin/courses/{id}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Delete a course";
            s.Description = "Permanently removes a course from the system. This action cannot be undone.";
            s.Response<ApiResponse<bool>>(200, "Course deleted successfully.");
            s.Response<ApiResponse<object>>(404, "The specified course ID could not be found.");
        });
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await courseService.DeleteAsync(id, ct);
        await result.Match(
            _ => SendSuccessAsync(true, ct, "Course deleted successfully."),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
