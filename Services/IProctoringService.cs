using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IProctoringService
{
    Task<ErrorOr<ExamProctoringSessionDto>> StartProctoringSessionAsync(Guid studentId, Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<ExamProctoringSessionDto>> UpdateProctoringHeartbeatAsync(Guid sessionId, DateTime heartbeatTimeUtc, string userIPAddress, CancellationToken ct = default);
    Task<ErrorOr<ExamProctoringSessionDto>> EndProctoringSessionAsync(Guid sessionId, CancellationToken ct = default);
}