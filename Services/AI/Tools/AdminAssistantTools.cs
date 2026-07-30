using System.ComponentModel;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services.AI.Tools;

public class AdminAssistantTools
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<AdminAssistantTools> _logger;

    public AdminAssistantTools(LmsDbContext dbContext, ILogger<AdminAssistantTools> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [Description("Returns a high-level system health overview: total users, students, courses, active session info.")]
    public async Task<string> GetSystemOverviewAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("AdminAssistantTools.GetSystemOverviewAsync called");

        var totalUsers = await _dbContext.Users.CountAsync(ct);
        var totalStudents = await _dbContext.Students.CountAsync(ct);
        var totalCourses = await _dbContext.Courses.CountAsync(ct);
        var totalCourseOfferings = await _dbContext.CourseOfferings.CountAsync(ct);
        var totalEnrollments = await _dbContext.CourseEnrollments.CountAsync(ct);

        var activeSessionName = await _dbContext.AcademicSessions
            .Where(s => s.IsActive)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);

        return $"System Overview:\n" +
               $"- Active Session: {activeSessionName ?? "None configured"}\n" +
               $"- Total Users: {totalUsers:N0}\n" +
               $"- Total Students: {totalStudents:N0}\n" +
               $"- Total Courses: {totalCourses:N0}\n" +
               $"- Course Offerings: {totalCourseOfferings:N0}\n" +
               $"- Total Enrollments: {totalEnrollments:N0}";
    }

    [Description("Returns user account statistics broken down by role.")]
    public async Task<string> GetUserStatsByRoleAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("AdminAssistantTools.GetUserStatsByRoleAsync called");

        var roleCounts = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.Role != null)
            .GroupBy(ur => ur.Role!.Name)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var totalUsersInDb = await _dbContext.Users.CountAsync(ct);
        var totalAssigned = roleCounts.Sum(r => r.Count);

        if (totalUsersInDb > totalAssigned)
        {
            roleCounts.Add(new { Role = "Standard User", Count = totalUsersInDb - totalAssigned });
        }

        var activeRoleCounts = roleCounts.Where(r => r.Count > 0).OrderByDescending(r => r.Count).ToList();

        if (activeRoleCounts.Count == 0)
            return "No registered user accounts found in the system.";

        var lines = activeRoleCounts.Select(g => $"- **{g.Role}**: {g.Count:N0} user(s)");
        return "Users by Role:\n" + string.Join("\n", lines);
    }

    [Description("Returns a list of recently registered users sorted by creation date.")]
    public async Task<string> GetRecentlyRegisteredUsersAsync(int count = 10, CancellationToken ct = default)
    {
        _logger.LogInformation("AdminAssistantTools.GetRecentlyRegisteredUsersAsync called");

        var users = await _dbContext.Users
            .OrderByDescending(u => u.CreatedUtc)
            .Take(count)
            .Select(u => new { u.DisplayName, u.Email, u.CreatedUtc, u.IsActive })
            .ToListAsync(ct);

        if (users.Count == 0)
            return "No users found.";

        var lines = users.Select(u =>
            $"- **{u.DisplayName ?? "N/A"}** ({u.Email ?? "N/A"}) — Joined: {u.CreatedUtc:yyyy-MM-dd} {(u.IsActive ? "✅" : "⛔")}");
        return $"Recently Registered Users ({users.Count}):\n" + string.Join("\n", lines);
    }

    [Description("Returns fee collection summary: total invoiced, total paid, outstanding balance, and collection rate for the active session.")]
    public async Task<string> GetFeeCollectionSummaryAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("AdminAssistantTools.GetFeeCollectionSummaryAsync called");

        var activeSession = await _dbContext.AcademicSessions.Where(s => s.IsActive).FirstOrDefaultAsync(ct);

        var feeQuery = _dbContext.StudentFeeRecords.AsQueryable();
        if (activeSession != null)
            feeQuery = feeQuery.Where(f => f.SessionId == activeSession.Id);

        var totalInvoiced = await feeQuery.SumAsync(f => f.TotalAmount, ct);
        var totalPaid = await feeQuery.SumAsync(f => f.AmountPaid, ct);
        var recordCount = await feeQuery.CountAsync(ct);

        if (recordCount == 0)
            return "No fee records found for the active session.";

        var outstanding = totalInvoiced - totalPaid;
        var collectionRate = totalInvoiced > 0 ? totalPaid / totalInvoiced * 100m : 0m;

        return $"Fee Collection Summary ({activeSession?.Name ?? "All Sessions"}):\n" +
               $"- Total Invoiced: ₦{totalInvoiced:N2} ({recordCount:N0} records)\n" +
               $"- Total Paid: ₦{totalPaid:N2}\n" +
               $"- Outstanding: ₦{outstanding:N2}\n" +
               $"- Collection Rate: {collectionRate:F1}%";
    }

    [Description("Returns enrollment statistics: total enrollments, unique students, and top 5 courses by enrollment.")]
    public async Task<string> GetEnrollmentStatisticsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("AdminAssistantTools.GetEnrollmentStatisticsAsync called");

        var totalEnrollments = await _dbContext.CourseEnrollments.CountAsync(ct);
        var uniqueStudents = await _dbContext.CourseEnrollments.Select(e => e.StudentId).Distinct().CountAsync(ct);

        var topCourseGroups = await _dbContext.CourseEnrollments
            .GroupBy(e => e.CourseOfferingId)
            .Select(g => new { CourseOfferingId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        var offeringIds = topCourseGroups.Select(c => c.CourseOfferingId).ToList();
        var offerings = await _dbContext.CourseOfferings
            .Where(o => offeringIds.Contains(o.Id))
            .Include(o => o.Course)
            .ToDictionaryAsync(o => o.Id, o => o.Course != null ? $"{o.Course.Code} — {o.Course.Title}" : "Unknown", ct);

        var topLines = topCourseGroups.Select(c =>
        {
            var title = offerings.TryGetValue(c.CourseOfferingId, out var t) ? t : "Unknown";
            return $"  - {title}: {c.Count} enrolled";
        });

        return $"Enrollment Statistics:\n" +
               $"- Total Enrollments: {totalEnrollments:N0}\n" +
               $"- Unique Students Enrolled: {uniqueStudents:N0}\n" +
               $"Top 5 Courses by Enrollment:\n" + string.Join("\n", topLines);
    }

    [Description("Returns recent system audit log entries including action, entity affected, and who performed it.")]
    public async Task<string> GetRecentAuditLogAsync(int count = 10, CancellationToken ct = default)
    {
        _logger.LogInformation("AdminAssistantTools.GetRecentAuditLogAsync called");

        var logs = await _dbContext.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync(ct);

        if (logs.Count == 0)
            return "No recent audit log entries found.";

        var lines = logs.Select(l =>
            $"- [{l.Timestamp:MM-dd HH:mm}] **{l.Action}** on {l.EntityName} by {l.User?.Email ?? l.User?.DisplayName ?? "System"}" +
            (l.Path != null ? $" ({l.HttpMethod} {l.Path})" : ""));
        return $"Recent System Audit Log ({logs.Count} entries):\n" + string.Join("\n", lines);
    }
}
