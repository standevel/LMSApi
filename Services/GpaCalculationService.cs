using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class GpaCalculationService : BaseService, IGpaCalculationService
{
    private readonly LmsDbContext _dbContext;

    public GpaCalculationService(LmsDbContext dbContext, IAuditService auditService) : base(auditService)
    {
        _dbContext = dbContext;
    }

    public async Task<ErrorOr<GpaDto>> GetStudentGpaAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .Include(x => x.AcademicProgram)
            .Include(x => x.Level)
            .FirstOrDefaultAsync(x => x.Id == studentId, ct);

        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        var result = await CalculateGpaForStudentAsync(studentId, null, ct);
        return result;
    }

    public async Task<ErrorOr<List<SessionGpaDto>>> GetStudentSessionGpasAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .FirstOrDefaultAsync(x => x.Id == studentId, ct);

        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        // Get all course offerings the student is enrolled in via ProgramEnrollment
        var enrollments = await _dbContext.Enrollments
            .Where(e => e.UserId == studentId)
            .Select(e => new { e.ProgramId, e.LevelId, e.AcademicSessionId })
            .ToListAsync(ct);

        var sessionGpas = new List<SessionGpaDto>();

        foreach (var enrollment in enrollments)
        {
            var session = await _dbContext.AcademicSessions
                .FirstOrDefaultAsync(s => s.Id == enrollment.AcademicSessionId, ct);

            if (session == null) continue;

            // Get all grades for this student in this session
            var courseOfferingIds = await _dbContext.CourseOfferings
                .Where(co => co.ProgramId == enrollment.ProgramId
                    && co.LevelId == enrollment.LevelId
                    && co.AcademicSessionId == enrollment.AcademicSessionId)
                .Select(co => co.Id)
                .ToListAsync(ct);

            var assessmentIds = await _dbContext.Assessments
                .Where(a => courseOfferingIds.Contains(a.CourseOfferingId))
                .Select(a => a.Id)
                .ToListAsync(ct);

            var grades = await _dbContext.Grades
                .Where(g => g.StudentId == studentId && assessmentIds.Contains(g.AssessmentId))
                .ToListAsync(ct);

            if (!grades.Any()) continue;

            var sessionGpa = CalculateGpa(grades);
            var creditsEarned = await CalculateCreditsEarned(grades, ct);

            sessionGpas.Add(new SessionGpaDto(
                studentId,
                session.Name,
                sessionGpa,
                grades.Count,
                creditsEarned,
                session.StartDate));
        }

        return sessionGpas.OrderByDescending(x => x.SessionDate).ToList();
    }

    public async Task<ErrorOr<GpaDto>> CalculateGpaForStudentAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .Include(x => x.AcademicProgram)
            .Include(x => x.Level)
            .FirstOrDefaultAsync(x => x.Id == studentId, ct);

        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        // Get all grades for this student
        IQueryable<Grade> gradesQuery = _dbContext.Grades
            .Where(g => g.StudentId == studentId)
            .Include(g => g.Assessment)
                .ThenInclude(a => a!.CourseOffering)
                    .ThenInclude(co => co!.AcademicSession);

        if (academicSessionId.HasValue)
        {
            gradesQuery = gradesQuery.Where(g =>
                g.Assessment!.CourseOffering!.AcademicSessionId == academicSessionId.Value);
        }

        var grades = await gradesQuery.ToListAsync(ct);

        if (!grades.Any())
            return DomainErrors.Reporting.GpaNotAvailable;

        var cumulativeGpa = CalculateGpa(grades);
        var creditsEarned = await CalculateCreditsEarned(grades, ct);

        // Get the most recent academic session
        var academicSession = academicSessionId.HasValue
            ? await _dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.Id == academicSessionId.Value, ct)
            : await _dbContext.AcademicSessions
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync(ct);

        // Get student's academic standing
        var standing = await _dbContext.AcademicStandings
            .Where(s => s.StudentId == studentId && (s.ExpiryDate == null || s.ExpiryDate > DateTime.UtcNow))
            .OrderByDescending(s => s.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        var standingType = standing?.StandingType.ToString() ?? "GoodStanding";

        return new GpaDto(
            studentId,
            $"{student.FirstName} {student.LastName}",
            student.OfficialEmail,
            cumulativeGpa,
            grades.Count,
            creditsEarned,
            creditsEarned > 0 ? cumulativeGpa : 0,
            academicSession?.Name ?? "N/A",
            standingType,
            DateTime.UtcNow);
    }

    private decimal CalculateGpa(IEnumerable<Grade> grades)
    {
        if (!grades.Any()) return 0;

        const decimal creditUnits = 3m; // Default credit units per course
        decimal totalPoints = 0;
        decimal totalCredits = 0;

        foreach (var grade in grades)
        {
            if (grade.IsLocked || grade.MarksObtained < 40) continue; // Only count passed, non-locked grades

            totalPoints += grade.MarksObtained * creditUnits;
            totalCredits += creditUnits;
        }

        return totalCredits > 0 ? Math.Round(totalPoints / totalCredits, 2) : 0;
    }

    private async Task<int> CalculateCreditsEarned(IEnumerable<Grade> grades, CancellationToken ct)
    {
        var gradeList = grades.ToList();
        var assessmentIds = gradeList.Select(g => g.AssessmentId).ToList();

        var assessments = await _dbContext.Assessments
            .Include(a => a.CourseOffering)
            .Where(a => assessmentIds.Contains(a.Id))
            .ToListAsync(ct);

        int creditsEarned = 0;
        foreach (var grade in gradeList)
        {
            if (grade.IsLocked || grade.MarksObtained < 40) continue;

            var assessment = assessments.FirstOrDefault(a => a.Id == grade.AssessmentId);
            if (assessment?.CourseOffering != null)
            {
                creditsEarned += assessment.CourseOffering.Course?.CreditUnits ?? 3;
            }
        }

        return creditsEarned;
    }
}
