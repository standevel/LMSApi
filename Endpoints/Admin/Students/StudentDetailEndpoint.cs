using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class StudentDetailEndpoint : ApiEndpointWithoutRequest<StudentDetailDto?>
{
    private readonly IStudentService _studentService;

    public StudentDetailEndpoint(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public override void Configure()
    {
        Get("admin/students/{id:guid}");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var student = await _studentService.GetStudentDetailAsync(id, ct);

        if (student == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await SendSuccessAsync(student, ct);
    }
}
