using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class ChangeStudentLevelEndpoint(
    IStudentService studentService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<ChangeStudentLevelRequest, ChangeStudentLevelItemResult>
{
    public override void Configure()
    {
        Put("admin/students/{id:guid}/level");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(ChangeStudentLevelRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = await currentUserContext.GetUserIdAsync(ct);

        var result = await studentService.ChangeStudentLevelAsync(id, req, userId, ct);

        if (!result.Success)
        {
            await SendFailureAsync(400, result.Message ?? "Failed to change student level.", "LEVEL_CHANGE_FAILED", result.Message ?? "Validation failed.", ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message ?? "Student level updated successfully.");
    }
}
