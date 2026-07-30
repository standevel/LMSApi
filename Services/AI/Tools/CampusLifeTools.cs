using System.ComponentModel;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services.AI.Tools;

public class CampusLifeTools
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<CampusLifeTools> _logger;

    public CampusLifeTools(LmsDbContext dbContext, ILogger<CampusLifeTools> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [Description("Retrieves the student's hostel bed allocation and block details for the current session.")]
    public async Task<string> GetStudentHostelRoomAllocationAsync(Guid studentId, CancellationToken ct = default)
    {
        _logger.LogInformation("CampusLifeTools querying hostel allocation for {StudentId}", studentId);

        var allocation = await _dbContext.HostelAllocations
            .Where(a => a.StudentId == studentId && a.Status == Data.Enums.AllocationStatus.Allocated)
            .Include(a => a.HostelBed)
                .ThenInclude(b => b!.HostelRoom)
                    .ThenInclude(r => r!.HostelBlock)
            .OrderByDescending(a => a.AllocatedAt)
            .FirstOrDefaultAsync(ct);

        if (allocation == null)
        {
            return "No active hostel bed allocation found. Please apply for accommodation via the Student Housing portal if you require on-campus housing.";
        }

        var bed = allocation.HostelBed;
        var room = bed?.HostelRoom;
        var block = room?.HostelBlock;

        return $"Hostel Allocation: Block '{block?.Name ?? "N/A"}', Room {room?.RoomNumber ?? "N/A"}, Bed {bed?.BedLabel ?? "N/A"}. " +
               $"Status: {allocation.Status}. Allocated: {allocation.AllocatedAt:yyyy-MM-dd}.";
    }

    [Description("Retrieves today's lecture sessions for the student's active course enrollments.")]
    public async Task<string> GetLecturesAndTimetableTodayAsync(Guid studentId, CancellationToken ct = default)
    {
        _logger.LogInformation("CampusLifeTools querying today's timetable for student {StudentId}", studentId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var enrolledOfferingIds = await _dbContext.CourseEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "Registered")
            .Select(e => e.CourseOfferingId)
            .ToListAsync(ct);

        if (enrolledOfferingIds.Count == 0)
        {
            return "You are not currently registered in any active course offerings for this semester.";
        }

        var sessions = await _dbContext.LectureSessions
            .Where(l => enrolledOfferingIds.Contains(l.CourseOfferingId) && l.SessionDate == today)
            .Include(l => l.CourseOffering)
                .ThenInclude(c => c.Course)
            .Include(l => l.Venue)
            .OrderBy(l => l.StartTime)
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            return "No lecture sessions scheduled for today. Take this time to revise your course materials!";
        }

        var schedule = string.Join("\n", sessions.Select(s =>
            $"- **{s.CourseOffering.Course?.Code ?? ""}** {s.CourseOffering.Course?.Title ?? ""}: " +
            $"{s.StartTime:hh\\:mm} – {s.EndTime:hh\\:mm}" +
            (s.Venue != null ? $" @ {s.Venue.Name}" : "") +
            (s.Notes != null ? $" ({s.Notes})" : "")));

        return $"Today's Schedule ({today:dddd, MMMM d}) — {sessions.Count} session(s):\n{schedule}";
    }

    [Description("Checks attendance percentage per course and exam eligibility status (75% threshold).")]
    public async Task<string> CheckStudentAttendanceEligibilityAsync(Guid studentId, CancellationToken ct = default)
    {
        _logger.LogInformation("CampusLifeTools checking attendance eligibility for student {StudentId}", studentId);

        var enrolledOfferingIds = await _dbContext.CourseEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "Registered")
            .Select(e => e.CourseOfferingId)
            .ToListAsync(ct);

        if (enrolledOfferingIds.Count == 0)
        {
            return "No active course enrollments found for attendance audit.";
        }

        var allSessions = await _dbContext.LectureSessions
            .Where(l => enrolledOfferingIds.Contains(l.CourseOfferingId) && l.IsCompleted)
            .Include(l => l.CourseOffering).ThenInclude(c => c.Course)
            .Include(l => l.Attendance)
            .ToListAsync(ct);

        if (allSessions.Count == 0)
        {
            return "No completed lecture sessions found yet. Attendance will be tracked once classes begin.";
        }

        var grouped = allSessions
            .GroupBy(s => new { s.CourseOfferingId, Code = s.CourseOffering.Course?.Code ?? "Course" })
            .ToList();

        var reportLines = grouped.Select(g =>
        {
            int total = g.Count();
            int present = g.Sum(s => s.Attendance.Count(a => a.StudentId == studentId && a.IsPresent));
            decimal pct = total > 0 ? (decimal)present / total * 100m : 100m;
            string eligibility = pct >= 75m
                ? "✅ Eligible for Final Exam"
                : "⚠️ At Risk — Below 75% Threshold";
            return $"- **{g.Key.Code}**: {pct:F1}% ({present}/{total} sessions attended) — {eligibility}";
        });

        return "Attendance & Exam Eligibility Audit:\n" + string.Join("\n", reportLines);
    }
}
