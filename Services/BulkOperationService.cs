using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using LMS.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class BulkOperationService : BaseService, IBulkOperationService
{
    private readonly LmsDbContext _context;

    public BulkOperationService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<BulkOperationDto>> CreateOperationAsync(CreateBulkOperationRequest request, Guid createdById, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.OperationType))
        {
            return Error.Validation("InvalidInput", "Operation type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return Error.Validation("InvalidInput", "File name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FileUrl))
        {
            return Error.Validation("InvalidInput", "File URL is required.");
        }

        var operation = new BulkOperation
        {
            OperationType = request.OperationType,
            FileName = request.FileName,
            FileUrl = request.FileUrl,
            Status = LMS.Api.Data.Entities.BulkOperationStatus.Pending,
            TotalRecords = 0, // Will be updated when processing starts
            ProcessedRecords = 0,
            FailedRecords = 0,
            CreatedById = createdById
        };

        _context.BulkOperations.Add(operation);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("CreateBulkOperation", "BulkOperation", operation.Id.ToString(),
            $"Bulk operation created: {request.OperationType}", ct);

        return MapToDto(operation);
    }

    public async Task<ErrorOr<Deleted>> ProcessBulkOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var operation = await _context.BulkOperations.FindAsync(new object[] { operationId }, ct);
        if (operation == null)
        {
            return Error.NotFound("BulkOperation.NotFound", "Bulk operation not found.");
        }

        if (operation.Status != LMS.Api.Data.Entities.BulkOperationStatus.Pending)
        {
            return Error.Validation("InvalidStatus", "Only pending bulk operations can be processed.");
        }

        // Update status to processing
        operation.Status = LMS.Api.Data.Entities.BulkOperationStatus.Processing;
        await _context.SaveChangesAsync(ct);

        try
        {
            // In a real implementation, we would download the file and process it based on the operation type
            // For now, just simulate some work and complete successfully
            await Task.Delay(2000, ct); // Simulate processing time

            // Update with results
            operation.Status = LMS.Api.Data.Entities.BulkOperationStatus.Completed;
            operation.TotalRecords = 100; // Placeholder
            operation.ProcessedRecords = 95; // Placeholder
            operation.FailedRecords = 5; // Placeholder
            operation.ErrorMessage = null;
            operation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            await LogActionAsync("ProcessBulkOperation", "BulkOperation", operationId.ToString(),
                $"Bulk operation processed successfully: {operation.OperationType}", ct);

            return Result.Deleted;
        }
        catch (Exception ex)
        {
            // Mark as failed
            operation.Status = LMS.Api.Data.Entities.BulkOperationStatus.Failed;
            operation.ErrorMessage = ex.Message;
            operation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            await LogActionAsync("ProcessBulkOperationFailed", "BulkOperation", operationId.ToString(),
                $"Bulk operation failed: {ex.Message}", ct);

            return Error.Validation("ProcessingFailed", $"Bulk operation processing failed: {ex.Message}");
        }
    }

    public async Task<ErrorOr<List<BulkOperationDto>>> GetOperationsAsync(LMS.Api.Data.Entities.BulkOperationStatus? status = null, CancellationToken ct = default)
    {
        IQueryable<BulkOperation> query = _context.BulkOperations;

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var operations = await query
            .Include(o => o.CreatedBy)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        return operations.Select(o => MapToDto(o)).ToList();
    }

    public async Task<ErrorOr<BulkOperationDto>> GetByIdAsync(Guid operationId, CancellationToken ct = default)
    {
        var operation = await _context.BulkOperations
            .FirstOrDefaultAsync(o => o.Id == operationId, ct);

        if (operation == null)
            return Error.NotFound("BulkOperation.NotFound", "Bulk operation not found.");

        return MapToDto(operation, includeResultData: true);
    }

    public async Task UpdateImportResultAsync(Guid operationId, StudentImportResponse result, CancellationToken ct = default)
    {
        var operation = await _context.BulkOperations.FirstOrDefaultAsync(o => o.Id == operationId, ct);
        if (operation == null) return;

        operation.Status = result.Status == "Failed"
            ? LMS.Api.Data.Entities.BulkOperationStatus.Failed
            : LMS.Api.Data.Entities.BulkOperationStatus.Completed;
        operation.TotalRecords = result.TotalRows;
        operation.ProcessedRecords = result.ProcessedRows;
        operation.FailedRecords = result.FailedRows;
        operation.ResultData = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = result.Status,
            errors = result.Errors.Select(e => new { e.RowNumber, e.Email, e.Reason }).ToList()
        });
        operation.ErrorMessage = result.FailedRows > 0
            ? $"{result.FailedRows} row(s) failed during import"
            : null;
        operation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(
        Guid operationId,
        LMS.Api.Data.Entities.BulkOperationStatus status,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var operation = await _context.BulkOperations.FirstOrDefaultAsync(o => o.Id == operationId, ct);
        if (operation == null) return;

        operation.Status = status;
        operation.ErrorMessage = errorMessage;
        operation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    private static BulkOperationDto MapToDto(BulkOperation o, bool includeResultData = false) =>
        new(
            o.Id,
            o.OperationType,
            o.FileName,
            (LMS.Api.Contracts.BulkOperationStatus)o.Status,
            o.TotalRecords,
            o.ProcessedRecords,
            o.FailedRecords,
            o.ErrorMessage,
            o.CreatedAt,
            o.UpdatedAt,
            includeResultData ? o.ResultData : null);
}