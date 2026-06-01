using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IPrerequisiteValidationService
{
    Task<ErrorOr<bool>> CheckPrerequisitesAsync(Guid studentId, Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> ProcessOverrideRequestAsync(Guid requestId, bool approvalGranted, string adminNotes, CancellationToken ct = default);
    Task<ErrorOr<PrerequisiteOverrideDto>> CreateOverrideRequestAsync(Guid studentId, Guid courseOfferingId, string reason, CancellationToken ct = default);
}