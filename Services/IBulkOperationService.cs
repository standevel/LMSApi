using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IBulkOperationService
{
    Task<ErrorOr<BulkOperationDto>> CreateOperationAsync(CreateBulkOperationRequest request, Guid createdById, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> ProcessBulkOperationAsync(Guid operationId, CancellationToken ct = default);
    Task<ErrorOr<List<BulkOperationDto>>> GetOperationsAsync(LMS.Api.Data.Entities.BulkOperationStatus? status = null, CancellationToken ct = default);
    Task<ErrorOr<BulkOperationDto>> GetByIdAsync(Guid operationId, CancellationToken ct = default);
    Task UpdateImportResultAsync(Guid operationId, StudentImportResponse result, CancellationToken ct = default);
    Task SetStatusAsync(Guid operationId, LMS.Api.Data.Entities.BulkOperationStatus status, string? errorMessage = null, CancellationToken ct = default);
}