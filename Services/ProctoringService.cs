using System;
using System.Collections.Generic;
using System.Linq;
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

    public Task<ErrorOr<ExamProctoringSessionDto>> StartProctoringSessionAsync(Guid studentId, Guid quizId, CancellationToken ct = default)
    {
        return StartProctoringSessionAsync(studentId, quizId, new StartProctoringRequest { QuizId = quizId }, ct);
    }

    public async Task<ErrorOr<ExamProctoringSessionDto>> StartProctoringSessionAsync(Guid studentId, Guid quizId, StartProctoringRequest request, CancellationToken ct = default)
    {
        var student = await _context.Users.FindAsync(new object[] { studentId }, ct);
        var quiz = await _context.Quizzes.FindAsync(new object[] { quizId }, ct);

        if (student == null)
            return Error.NotFound("Student.NotFound", "Student not found.");

        if (quiz == null)
            return Error.NotFound("Quiz.NotFound", "Quiz not found.");

        var existingSession = await _context.ExamProctoringSessions
            .FirstOrDefaultAsync(s => s.StudentId == studentId && s.QuizId == quizId && s.Status == "Active", ct);

        if (existingSession != null)
            return Error.Conflict("Proctoring.AlreadyActive", "An active proctoring session already exists.");

        var session = new ExamProctoringSession
        {
            StudentId = studentId,
            QuizId = quizId,
            Title = "Proctoring Session - " + (student.DisplayName ?? student.Email ?? student.Id.ToString()) + " - " + quiz.Title,
            Description = "Auto-generated proctoring session for quiz: " + quiz.Title,
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = null,
            Status = "Active",
            ViolationCount = 0,
            TabSwitchCount = 0,
            FullscreenLossCount = 0,
            IntegrityScore = 100m,
            CameraPermissionGranted = request.CameraPermissionGranted,
            SelfieCaptureUrl = request.SelfieCaptureUrl,
            BrowserInfo = request.BrowserInfo,
            ScreenResolution = request.ScreenResolution,
            UserAgent = request.UserAgent,
            IPAddress = request.IPAddress
        };

        _context.ExamProctoringSessions.Add(session);
        await _context.SaveChangesAsync(ct);

        return MapToDto(session);
    }

    public async Task<ErrorOr<ExamProctoringSessionDto>> UpdateProctoringHeartbeatAsync(Guid sessionId, UpdateProctoringHeartbeatRequest request, CancellationToken ct = default)
    {
        var session = await _context.ExamProctoringSessions
            .Include(s => s.Student)
            .Include(s => s.Quiz)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null || session.Status != "Active")
            return Error.NotFound("Proctoring.NotFound", "Proctoring session not found or is not active.");

        if (request.TabSwitchCount > session.TabSwitchCount)
        {
            var newSwitches = request.TabSwitchCount - session.TabSwitchCount;
            session.TabSwitchCount = request.TabSwitchCount;
            session.ViolationCount += newSwitches;
            for (var i = 0; i < newSwitches; i++)
            {
                _context.ProctoringViolations.Add(new ProctoringViolation
                {
                    SessionId = sessionId,
                    ViolationType = "TabSwitch",
                    Details = "Student switched away from the quiz window.",
                    Severity = 2
                });
            }
        }

        if (request.FullscreenLossCount > session.FullscreenLossCount)
        {
            var newFullscreenLosses = request.FullscreenLossCount - session.FullscreenLossCount;
            session.FullscreenLossCount = request.FullscreenLossCount;
            session.ViolationCount += newFullscreenLosses;
            for (var i = 0; i < newFullscreenLosses; i++)
            {
                _context.ProctoringViolations.Add(new ProctoringViolation
                {
                    SessionId = sessionId,
                    ViolationType = "FullscreenExit",
                    Details = "Student left fullscreen mode during the quiz.",
                    Severity = 2
                });
            }
        }
        else if (!request.IsFullscreen && session.IsFullscreen)
        {
            session.FullscreenLossCount++;
            session.ViolationCount++;
            _context.ProctoringViolations.Add(new ProctoringViolation
            {
                SessionId = sessionId,
                ViolationType = "FullscreenExit",
                Details = "Student left fullscreen mode during the quiz.",
                Severity = 2
            });
        }

        session.IsFullscreen = request.IsFullscreen;
        CalculateIntegrityScore(session);
        session.IPAddress = request.UserIPAddress;
        await _context.SaveChangesAsync(ct);
        return MapToDto(session);
    }

    public async Task<ErrorOr<ExamProctoringSessionDto>> EndProctoringSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.ExamProctoringSessions.FindAsync(new object[] { sessionId }, ct);
        if (session == null || session.Status != "Active")
            return Error.NotFound("Proctoring.NotFound", "Proctoring session not found or is already closed.");

        session.EndTimeUtc = DateTime.UtcNow;
        session.Status = "Completed";
        CalculateIntegrityScore(session);
        await _context.SaveChangesAsync(ct);
        return new ExamProctoringSessionDto(session.Id.ToString(), session.StartTimeUtc, session.EndTimeUtc.Value, "Completed");
    }
    public async Task<ErrorOr<ExamProctoringSessionDto>> RecordViolationAsync(Guid sessionId, RecordViolationRequest request, CancellationToken ct = default)
    {
        var session = await _context.ExamProctoringSessions
            .Include(s => s.Student)
            .Include(s => s.Quiz)
            .Include(s => s.Violations)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null || session.Status != "Active")
            return Error.NotFound("Proctoring.NotFound", "Proctoring session not found or is not active.");

        var violation = new ProctoringViolation
        {
            SessionId = sessionId,
            ViolationType = request.ViolationType,
            Details = request.Details,
            ScreenshotUrl = request.ScreenshotUrl,
            OccurredAtUtc = DateTime.UtcNow,
            Severity = request.Severity
        };

        session.Violations.Add(violation);
        session.ViolationCount++;
        CalculateIntegrityScore(session);
        await _context.SaveChangesAsync(ct);
        return MapToDto(session);
    }

    public async Task<ErrorOr<ProctoringSessionDto>> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.ExamProctoringSessions
            .Include(s => s.Student)
            .Include(s => s.Quiz)
            .Include(s => s.Violations)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
            return Error.NotFound("Proctoring.NotFound", "Proctoring session not found.");

        return MapToFullDto(session);
    }

    public async Task<ErrorOr<ProctoringLecturerDto>> GetLecturerDashboardAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FindAsync(new object[] { quizId }, ct);
        if (quiz == null)
            return Error.NotFound("Quiz.NotFound", "Quiz not found.");

        var courseOffering = await _context.CourseOfferings
            .FirstOrDefaultAsync(co => co.Id == quiz.CourseOfferingId, ct);

        if (courseOffering == null)
            return Error.NotFound("CourseOffering.NotFound", "Course offering not found.");

        var studentSummaries = new List<StudentProctoringSummary>();

        var enrollments = await _context.CourseEnrollments
            .Include(ce => ce.Student)
            .Where(ce => ce.CourseOfferingId == courseOffering.Id && ce.Status != "Dropped")
            .ToListAsync(ct);

        foreach (var enrollment in enrollments)
        {
            var student = enrollment.Student;
            if (student == null) continue;

            var session = await _context.ExamProctoringSessions
                .Include(s => s.Violations)
                .Where(s => s.StudentId == student.Id && s.QuizId == quizId)
                .OrderByDescending(s => s.StartTimeUtc)
                .FirstOrDefaultAsync(ct);

            var hasActiveAttempt = await _context.QuizAttempts
                .AnyAsync(a => a.StudentId == student.Id && a.QuizId == quizId && a.EndTime == null, ct);

            var summary = new StudentProctoringSummary
            {
                StudentId = student.Id,
                StudentName = student.DisplayName ?? student.Email ?? "",
                StudentEmail = student.Email ?? "",
                SessionId = session?.Id,
                SessionStatus = session?.Status,
                ViolationCount = session?.ViolationCount ?? 0,
                TabSwitchCount = session?.TabSwitchCount ?? 0,
                FullscreenLossCount = session?.FullscreenLossCount ?? 0,
                IntegrityScore = session?.IntegrityScore ?? 100m,
                StartTimeUtc = session?.StartTimeUtc,
                EndTimeUtc = session?.EndTimeUtc,
                CameraPermissionGranted = session?.CameraPermissionGranted ?? false,
                SelfieCaptureUrl = session?.SelfieCaptureUrl,
                HasActiveAttempt = hasActiveAttempt
            };
            studentSummaries.Add(summary);
        }

        var activeSessions = studentSummaries.Count(s => s.SessionStatus == "Active");
        var completedSessions = studentSummaries.Count(s => s.SessionStatus == "Completed");

        return new ProctoringLecturerDto
        {
            QuizId = quizId,
            QuizTitle = quiz.Title,
            TotalStudents = studentSummaries.Count,
            ActiveSessions = activeSessions,
            CompletedSessions = completedSessions,
            StudentSummaries = studentSummaries
        };
    }

    public async Task<ErrorOr<List<ProctoringSessionDto>>> GetSessionsByQuizAsync(Guid quizId, CancellationToken ct = default)
    {
        var sessions = await _context.ExamProctoringSessions
            .Include(s => s.Student)
            .Include(s => s.Quiz)
            .Include(s => s.Violations)
            .Where(s => s.QuizId == quizId)
            .OrderByDescending(s => s.StartTimeUtc)
            .ToListAsync(ct);

        return sessions.Select(MapToFullDto).ToList();
    }

    public async Task<ErrorOr<List<ExamProctoringSessionDto>>> ListSessionsAsync(Guid? quizId = null, CancellationToken ct = default)
    {
        var query = _context.ExamProctoringSessions
            .Include(s => s.Student)
            .Include(s => s.Quiz)
            .Include(s => s.Violations)
            .AsQueryable();

        if (quizId.HasValue)
        {
            query = query.Where(s => s.QuizId == quizId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.StartTimeUtc)
            .ToListAsync(ct);

        return sessions.Select(MapToDto).ToList();
    }

    private void CalculateIntegrityScore(ExamProctoringSession session)
    {
        var score = 100m;
        score -= Math.Min(session.TabSwitchCount * 2m, 30m);
        score -= Math.Min(session.FullscreenLossCount * 3m, 30m);
        score -= Math.Min(session.ViolationCount * 1m, 20m);
        session.IntegrityScore = Math.Max(0m, Math.Min(100m, score));
    }

    private ExamProctoringSessionDto MapToDto(ExamProctoringSession session)
    {
        return new ExamProctoringSessionDto(session.Id.ToString(), session.StartTimeUtc, session.Status)
        {
            StudentId = session.StudentId,
            QuizId = session.QuizId,
            EndTimeUtc = session.EndTimeUtc,
            ViolationCount = session.ViolationCount,
            TabSwitchCount = session.TabSwitchCount,
            FullscreenLossCount = session.FullscreenLossCount,
            IntegrityScore = session.IntegrityScore,
            CameraPermissionGranted = session.CameraPermissionGranted,
            SelfieCaptureUrl = session.SelfieCaptureUrl,
            BrowserInfo = session.BrowserInfo,
            ScreenResolution = session.ScreenResolution,
            IPAddress = session.IPAddress,
            StudentName = session.Student?.DisplayName ?? session.Student?.Email,
            StudentEmail = session.Student?.Email,
            QuizTitle = session.Quiz?.Title,
            Violations = session.Violations
                .OrderByDescending(v => v.OccurredAtUtc)
                .Select(v => new ProctoringViolationDto
                {
                    Id = v.Id,
                    SessionId = v.SessionId,
                    ViolationType = v.ViolationType,
                    Details = v.Details,
                    ScreenshotUrl = v.ScreenshotUrl,
                    OccurredAtUtc = v.OccurredAtUtc,
                    Severity = v.Severity
                })
                .ToList()
        };
    }

    private ProctoringSessionDto MapToFullDto(ExamProctoringSession session)
    {
        return new ProctoringSessionDto
        {
            Id = session.Id,
            Title = session.Title,
            Description = session.Description,
            StudentId = session.StudentId,
            StudentName = session.Student?.DisplayName ?? session.Student?.Email,
            StudentEmail = session.Student?.Email,
            QuizId = session.QuizId,
            QuizTitle = session.Quiz?.Title,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc,
            Status = session.Status,
            ViolationCount = session.ViolationCount,
            TabSwitchCount = session.TabSwitchCount,
            FullscreenLossCount = session.FullscreenLossCount,
            IntegrityScore = session.IntegrityScore,
            CameraPermissionGranted = session.CameraPermissionGranted,
            SelfieCaptureUrl = session.SelfieCaptureUrl,
            BrowserInfo = session.BrowserInfo,
            ScreenResolution = session.ScreenResolution,
            IPAddress = session.IPAddress,
            TotalViolations = session.Violations.Count,
            Violations = session.Violations
                .OrderByDescending(v => v.OccurredAtUtc)
                .Select(v => new ProctoringViolationDto
                {
                    Id = v.Id,
                    SessionId = v.SessionId,
                    ViolationType = v.ViolationType,
                    Details = v.Details,
                    ScreenshotUrl = v.ScreenshotUrl,
                    OccurredAtUtc = v.OccurredAtUtc,
                    Severity = v.Severity
                })
                .ToList()
        };
    }
}
