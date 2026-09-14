using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class BatchChangeStudentLevelEndpoint(
    IStudentService studentService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<BatchChangeStudentLevelRequest, BatchChangeStudentLevelResponse>
{
    public override void Configure()
    {
        Post("admin/students/batch-change-level");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(BatchChangeStudentLevelRequest req, CancellationToken ct)
    {
        if (req.StudentIds == null || req.StudentIds.Count == 0)
        {
            await SendFailureAsync(400, "Select at least one student.", "INVALID_REQUEST", "No students selected.", ct);
            return;
        }

        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await studentService.BatchChangeStudentLevelAsync(req, userId, ct);

        await SendSuccessAsync(result, ct, $"Successfully processed level update for {result.Successful} of {result.TotalRequested} students.");
    }
}
