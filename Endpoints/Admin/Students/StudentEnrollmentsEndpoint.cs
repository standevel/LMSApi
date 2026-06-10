using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class StudentEnrollmentsEndpoint : ApiEndpointWithoutRequest<IEnumerable<StudentEnrollmentDto>>
{
    private readonly IStudentService _studentService;

    public StudentEnrollmentsEndpoint(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public override void Configure()
    {
        Get("admin/students/{id:guid}/enrollments");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var enrollments = await _studentService.GetStudentEnrollmentsAsync(id, ct);
        await SendSuccessAsync(enrollments, ct);
    }
}
