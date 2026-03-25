using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Courses;

public record ToggleCourseStatusRequest(Guid Id);

public sealed class ToggleCourseStatusEndpoint(ICourseService courseService)
    : ApiEndpoint<ToggleCourseStatusRequest, CourseDto>
{
    public override void Configure()
    {
        Patch("/api/admin/courses/{id}/toggle-status");
    }

    public override async Task HandleAsync(ToggleCourseStatusRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await courseService.ToggleStatusAsync(id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
