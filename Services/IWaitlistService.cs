using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IWaitlistService
{
    Task<ErrorOr<WaitlistDto>> JoinWaitlistAsync(Guid studentId, Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> LeaveWaitlistAsync(Guid waitlistId, CancellationToken ct = default);
    Task<ErrorOr<List<WaitlistDto>>> GetStudentWaitlistsAsync(Guid studentId, CancellationToken ct = default);
}