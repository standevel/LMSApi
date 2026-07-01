using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using LMS.Api.Data;

namespace LMS.Api.Endpoints.Gradebook;

public record StudentDashboardStatsDto(
    int EnrolledCoursesCount,
    decimal CumulativeGpa,
    int AssignmentsDueThisWeek,
    string OverallProgressPercentage);

public record EnrolledCourseDto(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    string AcademicSessionName,
    int Semester,
    decimal ProgressPercentage,
    string LetterGrade,
    bool IsPublished);

public record StudentDashboardResponseDto(
    StudentDashboardStatsDto Stats,
    List<EnrolledCourseDto> EnrolledCourses,
    Guid StudentId,
    Guid AcademicSessionId,
    string? ProgramName = null,
    string? LevelName = null,
    string? StudentNumber = null);

public sealed class GetStudentDashboardEndpoint : ApiEndpointWithoutRequest<StudentDashboardResponseDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly LmsDbContext _dbContext;

    public GetStudentDashboardEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext, LmsDbContext dbContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get("gradebook/my-dashboard");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        // Get all published grades for the student
        var allGrades = await _gradebookService.GetStudentAllGradesAsync(userId.Value, ct);
        if (allGrades.IsError)
        {
            await SendFailureAsync(400, "Bad request", "ERROR", allGrades.FirstError.Description, ct);
            return;
        }

        var gradeViews = allGrades.Value;

        // Get enrolled courses count from enrollments
        var enrollments = await _dbContext.CourseEnrollments
            .Where(e => e.StudentId == userId.Value && e.Status == "Registered")
            .ToListAsync(ct);

        var enrolledCoursesCount = enrollments.Count;

        // Calculate cumulative GPA from letter grades
        var gpaPoints = new Dictionary<string, decimal>
        {
            { "A", 5.0m }, { "B", 4.0m }, { "C", 3.0m }, { "D", 2.0m }, { "E", 1.0m }, { "F", 0.0m }
        };

        decimal cumulativeGpa = 0;
        if (gradeViews.Any())
        {
            cumulativeGpa = gradeViews
                .Select(g => gpaPoints.GetValueOrDefault(g.LetterGrade, 0m))
                .Average();
        }

        // Count assignments due this week from assessments
        var now = DateTime.UtcNow;
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

        var assessmentsDue = await _dbContext.Assessments
            .Where(a => a.DueDate.HasValue && a.DueDate.Value >= startOfWeek && a.DueDate.Value <= endOfWeek)
            .Include(a => a.Grades)
            .Where(a => a.Grades.Any(g => g.StudentId == userId.Value))
            .CountAsync(ct);

        // Calculate overall progress percentage
        decimal overallProgress = 0;
        if (gradeViews.Any())
        {
            overallProgress = gradeViews.Average(g => g.TotalScore);
        }

        var progressStr = overallProgress > 0 ? $"{overallProgress:F0}%" : "0%";

        var stats = new StudentDashboardStatsDto(
            enrolledCoursesCount,
            Math.Round(cumulativeGpa, 2),
            assessmentsDue,
            progressStr);

        // Build enrolled courses list from grade views (published courses with grades)
        var enrolledCourses = gradeViews.Select(g => new EnrolledCourseDto(
            g.CourseOfferingId,
            g.CourseCode,
            g.CourseTitle,
            g.AcademicSessionName,
            g.Semester,
            g.TotalScore,
            g.LetterGrade,
            g.IsPublished)).ToList();

        // Also pull current enrollments to show courses that haven't been graded yet
        var activeEnrollments = await _dbContext.CourseEnrollments
            .Where(e => e.StudentId == userId.Value && e.Status == "Registered")
            .Include(e => e.CourseOffering)
                .ThenInclude(o => o.Course)
            .Include(e => e.CourseOffering)
                .ThenInclude(o => o.AcademicSession)
            .ToListAsync(ct);

        var activeOfferings = activeEnrollments
            .Select(e => e.CourseOffering)
            .Where(o => o != null)
            .ToList();

        var publishedOfferingIds = new HashSet<Guid>(enrolledCourses.Select(c => c.CourseOfferingId));

        foreach (var offering in activeOfferings)
        {
            if (!publishedOfferingIds.Contains(offering.Id))
            {
                enrolledCourses.Add(new EnrolledCourseDto(offering.Id, offering.Course.Code, offering.Course.Title,
                    offering.AcademicSession.Name, (int)offering.Semester, 0, "N/A", false));
                publishedOfferingIds.Add(offering.Id);
            }
        }

        // Sort by session (newest first), then by course code
        enrolledCourses = enrolledCourses
            .OrderByDescending(c => c.AcademicSessionName)
            .ThenBy(c => c.CourseCode)
            .ToList();

        var student = await _dbContext.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Level)
            .FirstOrDefaultAsync(s => s.Id == userId.Value, ct);
        var academicSessionId = student?.AcademicSessionId 
            ?? activeOfferings.FirstOrDefault()?.AcademicSessionId 
            ?? (await _dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive, ct))?.Id 
            ?? Guid.Empty;

        var response = new StudentDashboardResponseDto(
            stats, 
            enrolledCourses, 
            userId.Value, 
            academicSessionId,
            student?.AcademicProgram?.Name,
            student?.Level?.Name,
            student?.StudentNumber);
        await SendSuccessAsync(response, ct);
    }
}
