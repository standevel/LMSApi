using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class StudentAssignmentsEndpoint : ApiEndpointWithoutRequest<IEnumerable<StudentAssignmentDto>>
{
    private readonly IStudentService _studentService;

    public StudentAssignmentsEndpoint(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public override void Configure()
    {
        Get("admin/students/{id:guid}/assignments");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var assignments = await _studentService.GetStudentAssignmentsAsync(id, ct);
        await SendSuccessAsync(assignments, ct);
    }
}
