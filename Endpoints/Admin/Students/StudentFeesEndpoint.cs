using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class StudentFeesEndpoint : ApiEndpointWithoutRequest<StudentFeesResponse>
{
    private readonly IStudentService _studentService;

    public StudentFeesEndpoint(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public override void Configure()
    {
        Get("admin/students/{id:guid}/fees");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var (records, payments) = await _studentService.GetStudentFeesAsync(id, ct);
        await SendSuccessAsync(new StudentFeesResponse
        {
            Records = records,
            Payments = payments
        }, ct);
    }
}
