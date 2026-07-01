using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IProctoringService
{
    Task<ErrorOr<ExamProctoringSessionDto>> StartProctoringSessionAsync(Guid studentId, Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<ExamProctoringSessionDto>> StartProctoringSessionAsync(Guid studentId, Guid quizId, StartProctoringRequest request, CancellationToken ct = default);
    Task<ErrorOr<ExamProctoringSessionDto>> UpdateProctoringHeartbeatAsync(Guid sessionId, UpdateProctoringHeartbeatRequest request, CancellationToken ct = default);
    Task<ErrorOr<ExamProctoringSessionDto>> EndProctoringSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<ErrorOr<ExamProctoringSessionDto>> RecordViolationAsync(Guid sessionId, RecordViolationRequest request, CancellationToken ct = default);
    Task<ErrorOr<ProctoringSessionDto>> GetSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<ErrorOr<ProctoringLecturerDto>> GetLecturerDashboardAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<List<ProctoringSessionDto>>> GetSessionsByQuizAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<List<ExamProctoringSessionDto>>> ListSessionsAsync(Guid? quizId = null, CancellationToken ct = default);
}
