using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class BatchChangeStudentProgramEndpoint(
    IStudentService studentService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<BatchChangeStudentProgramRequest, BatchChangeStudentProgramResponse>
{
    public override void Configure()
    {
        Post("admin/students/batch-change-program");
        Roles("Admin", "SuperAdmin", "Registrar");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(BatchChangeStudentProgramRequest req, CancellationToken ct)
    {
        if (req.StudentIds == null || req.StudentIds.Count == 0)
        {
            await SendFailureAsync(400, "Select at least one student.", "INVALID_REQUEST", "No students selected.", ct);
            return;
        }

        var userId = await currentUserContext.GetUserIdAsync(ct);
        var result = await studentService.BatchChangeStudentProgramAsync(req, userId, ct);

        await SendSuccessAsync(result, ct, $"Successfully processed program update for {result.Successful} of {result.TotalRequested} students.");
    }
}
