using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.BulkOperations;

public sealed class StudentImportEndpoint(
    IBulkOperationService bulkOperationService,
    IStudentBulkImportService studentImportService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<StudentImportEndpoint.Request, StudentImportResponse>
{
    public sealed class Request
    {
        public List<StudentImportRowDto> Students { get; set; } = [];
        public Guid? DefaultSessionId { get; set; }
    }

    public override void Configure()
    {
        Post("bulk-operations/students/import");
        Roles("SuperAdmin", "Admin", "Registrar");
        Tags("BulkOperations");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            if (!userId.HasValue)
            {
                await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
                return;
            }

            if (req.Students.Count == 0)
            {
                await SendFailureAsync(400, "Bad Request", "INVALID_DATA", "At least one student row is required", ct);
                return;
            }

            var createReq = new CreateBulkOperationRequest(
                OperationType: "StudentImport",
                FileName: $"student-import-{DateTime.UtcNow:yyyyMMddHHmmss}.json",
                FileUrl: "json-payload");

            var operationResult = await bulkOperationService.CreateOperationAsync(createReq, userId.Value, ct);
            if (operationResult.IsError)
            {
                await SendFailureAsync(400, "Bad Request", "CREATE_OPERATION_FAILED", operationResult.Errors.First().Description, ct);
                return;
            }

            var bulkOperationId = operationResult.Value.Id;
            await bulkOperationService.SetStatusAsync(bulkOperationId, Data.Entities.BulkOperationStatus.Processing, ct: ct);

            StudentImportResponse importResult;
            try
            {
                importResult = await studentImportService.ImportStudentsFromRowsAsync(
                    req.Students, bulkOperationId, req.DefaultSessionId, ct);
            }
            catch (Exception ex)
            {
                await bulkOperationService.SetStatusAsync(bulkOperationId, Data.Entities.BulkOperationStatus.Failed, ex.Message, ct);
                await SendFailureAsync(500, "Import Failed", "IMPORT_FAILED", ex.Message, ct);
                return;
            }

            await bulkOperationService.UpdateImportResultAsync(bulkOperationId, importResult, ct);
            await SendSuccessAsync(importResult, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Import Failed", "IMPORT_FAILED", ex.Message, ct);
        }
    }
}
