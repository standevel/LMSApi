using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class ProctoringService : BaseService, IProctoringService
{
    private readonly LmsDbContext _context;

    public ProctoringService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<ExamProctoringSessionDto>> StartProctoringSessionAsync(Guid studentId, Guid quizId, CancellationToken ct = default)
    {
        var session = new ExamProctoringSession
        {
            StudentId = studentId,
            QuizId = quizId,
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = null,
            Status = "Active",
            ViolationCount = 0
        };
        _context.ExamProctoringSessions.Add(session);
        await _context.SaveChangesAsync(ct);
        return new ExamProctoringSessionDto(session.Id.ToString(), session.StartTimeUtc, "Active");
    }

    public async Task<ErrorOr<ExamProctoringSessionDto>> UpdateProctoringHeartbeatAsync(Guid sessionId, DateTime heartbeatTimeUtc, string userIPAddress, CancellationToken ct = default)
    {
        var session = await _context.ExamProctoringSessions.FindAsync(new object[] { sessionId }, ct);
        if (session == null || session.Status != "Active")
            return Error.NotFound("Proctoring.NotFound", "Proctoring session not found or is not active.");
        session.Status = "Active";
        await _context.SaveChangesAsync(ct);
        return new ExamProctoringSessionDto(session.Id.ToString(), session.StartTimeUtc, session.Status);
    }

    public async Task<ErrorOr<ExamProctoringSessionDto>> EndProctoringSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.ExamProctoringSessions.FindAsync(new object[] { sessionId }, ct);
        if (session == null || session.Status != "Active")
            return Error.NotFound("Proctoring.NotFound", "Proctoring session not found or is already closed.");

        session.EndTimeUtc = DateTime.UtcNow;
        session.Status = "Completed";
        await _context.SaveChangesAsync(ct);
        return new ExamProctoringSessionDto(session.Id.ToString(), session.StartTimeUtc, session.EndTimeUtc.Value, "Completed");
    }
}
