using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class StudentParentsEndpoint : ApiEndpointWithoutRequest<IEnumerable<StudentParentDto>>
{
    private readonly IStudentService _studentService;

    public StudentParentsEndpoint(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public override void Configure()
    {
        Get("admin/students/{id:guid}/parents");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var parents = await _studentService.GetStudentParentsAsync(id, ct);
        await SendSuccessAsync(parents, ct);
    }
}
