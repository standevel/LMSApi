using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Users;

public sealed class ListLecturersEndpoint(ICourseService courseService)
    : ApiEndpoint<EmptyRequest, List<SimpleUserDto>>
{
    public override void Configure()
    {
        Get("/api/admin/lecturers");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await courseService.GetLecturersAsync(ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
