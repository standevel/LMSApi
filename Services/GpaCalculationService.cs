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

        var enrollments = await _dbContext.CourseEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "Registered")
            .Select(e => e.CourseOffering.AcademicSessionId)
            .Distinct()
            .ToListAsync(ct);

        var sessionGpas = new List<SessionGpaDto>();

        foreach (var sessionId in enrollments)
        {
            var session = await _dbContext.AcademicSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

            if (session == null) continue;

            var gpaResult = await CalculateGpaForStudentAsync(studentId, sessionId, ct);
            if (gpaResult.IsError) continue;

            sessionGpas.Add(new SessionGpaDto(
                studentId,
                session.Name,
                gpaResult.Value.CumulativeGpa,
                gpaResult.Value.TotalCreditsAttempted,
                gpaResult.Value.TotalCreditsEarned,
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

        var sysConfig = await _dbContext.SystemGradingConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();

        // Get enrollments
        var enrollmentsQuery = _dbContext.CourseEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "Registered")
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.AcademicSession);

        var enrollments = await enrollmentsQuery.ToListAsync(ct);

        if (academicSessionId.HasValue)
        {
            enrollments = enrollments.Where(e => e.CourseOffering.AcademicSessionId == academicSessionId.Value).ToList();
        }

        if (!enrollments.Any())
            return DomainErrors.Reporting.GpaNotAvailable;

        // Process course grades
        decimal totalGpaPoints = 0;
        decimal totalGpaCredits = 0;
        int totalCreditsEarned = 0;
        int gradedCoursesCount = 0;

        foreach (var enrollment in enrollments)
        {
            var offering = enrollment.CourseOffering;
            var assessments = await _dbContext.Assessments
                .Where(a => a.CourseOfferingId == offering.Id)
                .Include(a => a.AssessmentCategory)
                .ToListAsync(ct);

            if (!assessments.Any()) continue;

            var studentGrades = await _dbContext.Grades
                .Where(g => g.StudentId == studentId && assessments.Select(a => a.Id).Contains(g.AssessmentId))
                .ToListAsync(ct);

            if (!studentGrades.Any()) continue;

            var totalScore = CalculateCourseScore(assessments, studentGrades, sysConfig, finalizedOnly: true);
            if (!totalScore.HasValue) continue;

            var roundedScore = Math.Round(totalScore.Value, 2);
            var gradePoints = ConvertToGradePoints(roundedScore, sysConfig);
            var creditUnits = offering.Course?.CreditUnits ?? 3;

            totalGpaPoints += gradePoints * creditUnits;
            totalGpaCredits += creditUnits;
            gradedCoursesCount++;

            if (gradePoints >= 1.0m)
            {
                totalCreditsEarned += creditUnits;
            }
        }

        if (gradedCoursesCount == 0)
            return DomainErrors.Reporting.GpaNotAvailable;

        var cumulativeGpa = totalGpaCredits > 0 ? Math.Round(totalGpaPoints / totalGpaCredits, 2) : 0;

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
            gradedCoursesCount,
            totalCreditsEarned,
            totalCreditsEarned > 0 ? cumulativeGpa : 0,
            academicSession?.Name ?? "N/A",
            standingType,
            DateTime.UtcNow);
    }

    private decimal ConvertToGradePoints(decimal marks, SystemGradingConfiguration sysConfig)
    {
        var mappings = string.IsNullOrEmpty(sysConfig.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
            ? new List<LMS.Api.Contracts.GradeMappingDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<LMS.Api.Contracts.GradeMappingDto>>(sysConfig.LetterGradesMappingJson) 
              ?? new List<LMS.Api.Contracts.GradeMappingDto>();
              
        if (mappings == null || !mappings.Any())
        {
            if (sysConfig.GpaScale == 5.0m)
            {
                return marks switch
                {
                    >= 70 => 5.0m,
                    >= 60 => 4.0m,
                    >= 50 => 3.0m,
                    >= 45 => 2.0m,
                    >= 40 => 1.0m,
                    _ => 0.0m
                };
            }

            return marks switch
            {
                >= 70 => 4.0m,
                >= 65 => 3.75m,
                >= 60 => 3.5m,
                >= 55 => 3.0m,
                >= 50 => 2.5m,
                >= 45 => 2.0m,
                >= 40 => 1.0m,
                _ => 0.0m
            };
        }
        
        var match = mappings.OrderByDescending(m => m.MinPercentage)
            .FirstOrDefault(m => marks >= m.MinPercentage);
            
        return match?.GradePoints ?? 0.0m;
    }

    private static decimal? CalculateCourseScore(
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyCollection<Grade> grades,
        SystemGradingConfiguration sysConfig,
        bool finalizedOnly)
    {
        var usableGrades = finalizedOnly
            ? grades.Where(g => g.IsLocked).ToList()
            : grades.ToList();

        if (usableGrades.Count == 0)
        {
            return null;
        }

        var percentages = assessments
            .Select(assessment =>
            {
                var grade = usableGrades.FirstOrDefault(g => g.AssessmentId == assessment.Id);
                if (grade == null || assessment.MaxMarks <= 0)
                {
                    return null;
                }

                return new AssessmentPercentage(
                    assessment.AssessmentCategoryId,
                    assessment.AssessmentCategory.Weight,
                    grade.MarksObtained / assessment.MaxMarks * 100m);
            })
            .Where(x => x != null)
            .Cast<AssessmentPercentage>()
            .ToList();

        if (percentages.Count == 0)
        {
            return null;
        }

        if (sysConfig.DefaultGradingStyle == GradingStyle.Unweighted)
        {
            return percentages.Average(x => x.Percentage);
        }

        return percentages
            .GroupBy(x => x.CategoryId)
            .Sum(category =>
            {
                var categoryAverage = category.Average(x => x.Percentage);
                var categoryWeight = category.First().CategoryWeight;
                return categoryAverage * categoryWeight / 100m;
            });
    }

    private sealed record AssessmentPercentage(Guid CategoryId, decimal CategoryWeight, decimal Percentage);
}
