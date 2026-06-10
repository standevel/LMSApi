using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class StudentResultsEndpoint : ApiEndpointWithoutRequest<IEnumerable<StudentCourseResultDto>>
{
    private readonly IStudentService _studentService;

    public StudentResultsEndpoint(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public override void Configure()
    {
        Get("admin/students/{id:guid}/results");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var results = await _studentService.GetStudentResultsAsync(id, ct);
        await SendSuccessAsync(results, ct);
    }
}
