using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class UpdateStudentDirectEntryEndpoint(
    IStudentService studentService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateStudentDirectEntryRequest, UpdateStudentDirectEntryResult>
{
    public override void Configure()
    {
        Put("admin/students/{id:guid}/direct-entry");
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(UpdateStudentDirectEntryRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var userId = await currentUserContext.GetUserIdAsync(ct);

        var result = await studentService.SetDirectEntryStatusAsync(id, req, userId, ct);

        if (!result.Success)
        {
            await SendFailureAsync(400, result.Message ?? "Failed to update direct entry status.", "DIRECT_ENTRY_UPDATE_FAILED", result.Message ?? "Validation failed.", ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message ?? "Student direct entry status updated successfully.");
    }
}
