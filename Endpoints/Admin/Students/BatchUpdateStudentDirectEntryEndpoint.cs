using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class BatchUpdateStudentDirectEntryEndpoint(
    IStudentService studentService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<BatchUpdateStudentDirectEntryRequest, BatchUpdateStudentDirectEntryResponse>
{
    public override void Configure()
    {
        Verbs(Http.POST, Http.PUT);
        Routes("admin/students/batch-direct-entry");
        Roles("Admin", "SuperAdmin", "Registrar", "AdmissionOfficer");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(BatchUpdateStudentDirectEntryRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);

        var response = await studentService.BatchSetDirectEntryStatusAsync(req, userId, ct);

        await SendSuccessAsync(response, ct, response.Message);
    }
}
