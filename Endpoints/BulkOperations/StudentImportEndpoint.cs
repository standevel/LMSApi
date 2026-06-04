using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.BulkOperations;

public sealed class StudentImportEndpoint(
    IBulkOperationService bulkOperationService,
    IStudentBulkImportService studentImportService,
    ICurrentUserContext currentUserContext,
    LmsDbContext context)
    : ApiEndpoint<StudentImportEndpoint.Request, StudentImportResponse>
{
    public sealed class Request
    {
        [FromForm] public IFormFile? CsvFile { get; set; }
        [FromForm] public Guid? DefaultSessionId { get; set; }
    }

    public override void Configure()
    {
        Post("bulk-operations/students/import");
        Roles("SuperAdmin", "Admin");
        Tags("BulkOperations");
        AllowFileUploads();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Validate file
        if (req.CsvFile == null || req.CsvFile.Length == 0)
        {
            await SendFailureAsync(400, "Bad Request", "INVALID_FILE", "CSV file is required", ct);
            return;
        }

        var extension = Path.GetExtension(req.CsvFile.FileName)?.ToLowerInvariant();
        if (extension != ".csv")
        {
            await SendFailureAsync(400, "Bad Request", "INVALID_FILE", "Only CSV files are accepted", ct);
            return;
        }

        // Step 1: Create a bulk operation record
        var createReq = new CreateBulkOperationRequest(
            OperationType: "StudentImport",
            FileName: req.CsvFile.FileName,
            FileUrl: req.CsvFile.FileName);

        var operationResult = await bulkOperationService.CreateOperationAsync(createReq, userId.Value, ct);
        if (operationResult.IsError)
        {
            await SendFailureAsync(400, "Bad Request", "CREATE_OPERATION_FAILED", operationResult.Errors.First().Description, ct);
            return;
        }

        var bulkOperationId = operationResult.Value.Id;

        // Step 2: Update operation status to processing
        var operation = await bulkOperationService.GetOperationsAsync(null, ct);
        // We need a different approach — update directly via context
        // For now, proceed with import and update after

        // Step 3: Process the CSV
        using var stream = req.CsvFile.OpenReadStream();
        var importResult = await studentImportService.ImportStudentsAsync(
            stream, bulkOperationId, req.DefaultSessionId, ct);

        // Step 4: Update bulk operation with results
        await UpdateBulkOperationAsync(bulkOperationId, importResult, ct);

        await SendAsync(importResult, ct);
    }

    private readonly LmsDbContext _context = context;

    private async Task UpdateBulkOperationAsync(Guid operationId, StudentImportResponse result, CancellationToken ct)
    {
        var operation = await _context.BulkOperations.FindAsync(new object[] { operationId }, ct);
        if (operation == null) return;

        operation.Status = Data.Entities.BulkOperationStatus.Completed;
        operation.TotalRecords = result.TotalRows;
        operation.ProcessedRecords = result.ProcessedRows;
        operation.FailedRecords = result.FailedRows;
        operation.ResultData = System.Text.Json.JsonSerializer.Serialize(new
        {
            errors = result.Errors.Select(e => new { e.RowNumber, e.Email, e.Reason })
        });
        operation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}
