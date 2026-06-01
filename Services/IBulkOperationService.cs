using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IBulkOperationService
{
    Task<ErrorOr<BulkOperationDto>> CreateOperationAsync(CreateBulkOperationRequest request, Guid createdById, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> ProcessBulkOperationAsync(Guid operationId, CancellationToken ct = default);
    Task<ErrorOr<List<BulkOperationDto>>> GetOperationsAsync(LMS.Api.Data.Entities.BulkOperationStatus? status = null, CancellationToken ct = default);
}