using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IScheduleService
{
    Task<ErrorOr<List<ScheduleDto>>> GetStudentScheduleAsync(Guid studentId, Guid academicSessionId, CancellationToken ct = default);
    Task<ErrorOr<ScheduleAdjustmentRequestDto>> RequestScheduleAdjustmentAsync(Guid studentId, string reason, string desiredSlotDetails, CancellationToken ct = default);
}