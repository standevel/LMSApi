using System.ComponentModel;
using LMS.Api.Data;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services.AI.Tools;

public class AdvisorAgentTools
{
    private readonly IGpaCalculationService _gpaService;
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<AdvisorAgentTools> _logger;

    public AdvisorAgentTools(
        IGpaCalculationService gpaService,
        LmsDbContext dbContext,
        ILogger<AdvisorAgentTools> logger)
    {
        _gpaService = gpaService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [Description("Calculates the cumulative and session-by-session GPA for a student.")]
    public async Task<string> GetStudentGpaSummaryAsync(Guid studentId)
    {
        _logger.LogInformation("AdvisorAgentTool calling GetStudentGpaSummaryAsync for {StudentId}", studentId);

        var student = await _dbContext.Students
            .FirstOrDefaultAsync(s => s.Id == studentId || s.EntraObjectId == studentId.ToString());

        if (student == null)
        {
            var appUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (appUser != null)
            {
                student = await _dbContext.Students.FirstOrDefaultAsync(s => s.OfficialEmail == appUser.Email || s.PersonalEmail == appUser.Email);
            }
        }

        if (student != null)
        {
            studentId = student.Id;
        }

        var gpaResult = await _gpaService.GetStudentGpaAsync(studentId);
        decimal cumulativeGpa = 0;
        int totalCreditsEarned = 0;
        int totalCreditsAttempted = 0;
        string standingType = "GoodStanding";
        string studentName = student != null ? $"{student.FirstName} {student.LastName}" : "Student";

        if (!gpaResult.IsError)
        {
            var gpa = gpaResult.Value;
            cumulativeGpa = gpa.CumulativeGpa;
            totalCreditsEarned = gpa.TotalCreditsEarned;
            totalCreditsAttempted = gpa.TotalCreditsAttempted;
            standingType = gpa.StandingType;
            studentName = gpa.StudentName;
        }
        else
        {
            // Direct DbContext GPA fallback
            var studentGrades = await _dbContext.Grades
                .Where(g => g.StudentId == studentId)
                .Include(g => g.Assessment)
                .ToListAsync();

            if (studentGrades.Count > 0)
            {
                var validGrades = studentGrades.Where(g => g.Assessment != null && g.Assessment.MaxMarks > 0).ToList();
                if (validGrades.Count > 0)
                {
                    decimal avgPct = validGrades.Average(g => (g.MarksObtained / g.Assessment.MaxMarks) * 100m);
                    cumulativeGpa = avgPct >= 70m ? 5.00m : avgPct >= 60m ? 4.00m : avgPct >= 50m ? 3.00m : 2.00m;
                    totalCreditsEarned = validGrades.Count * 3;
                    totalCreditsAttempted = totalCreditsEarned;
                    standingType = cumulativeGpa >= 4.5m ? "FirstClass" : cumulativeGpa >= 3.5m ? "SecondClassUpper" : "GoodStanding";
                }
            }
        }

        var sessionGpasResult = await _gpaService.GetStudentSessionGpasAsync(studentId);

        var response = $"Database Record for {studentName}: Current Cumulative GPA is {cumulativeGpa:F2} / 5.00 (Academic Standing: {standingType}, Earned Credits: {totalCreditsEarned}, Attempted Credits: {totalCreditsAttempted}).";
        
        if (!sessionGpasResult.IsError && sessionGpasResult.Value.Count > 0)
        {
            response += " Session Breakdown: " + string.Join("; ", sessionGpasResult.Value.Select(s => $"{s.AcademicSessionName}: {s.SessionGpa:F2} GPA"));
        }

        return response;
    }

    [Description("Projects projected cumulative GPA based on hypothetical target grades in upcoming courses.")]
    public string ProjectFutureGpa(double currentGpa, int currentTotalUnits, List<int> targetCourseUnits, List<double> targetGradePoints)
    {
        if (targetCourseUnits.Count != targetGradePoints.Count)
        {
            return "Unit count and grade point count array length mismatch.";
        }

        double totalCurrentPoints = currentGpa * currentTotalUnits;
        double newUnits = targetCourseUnits.Sum();
        double newPoints = 0;

        for (int i = 0; i < targetCourseUnits.Count; i++)
        {
            newPoints += targetCourseUnits[i] * targetGradePoints[i];
        }

        double projectedGpa = (totalCurrentPoints + newPoints) / (currentTotalUnits + newUnits);
        return $"Projected Cumulative GPA will be {projectedGpa:F2} across total {currentTotalUnits + newUnits} credit units.";
    }
}
