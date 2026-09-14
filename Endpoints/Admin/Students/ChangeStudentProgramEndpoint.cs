using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class ChangeStudentProgramEndpoint(
    IStudentService studentService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<ChangeStudentProgramRequest, ChangeStudentProgramItemResult>
{
    public override void Configure()
    {
        Put("admin/students/{id:guid}/program");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(ChangeStudentProgramRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = await currentUserContext.GetUserIdAsync(ct);

        var result = await studentService.ChangeStudentProgramAsync(id, req, userId, ct);

        if (!result.Success)
        {
            await SendFailureAsync(400, result.Message ?? "Failed to update student program.", "PROGRAM_CHANGE_FAILED", result.Message ?? "Validation failed.", ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message ?? "Student program updated successfully.");
    }
}
