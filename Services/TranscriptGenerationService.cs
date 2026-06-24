using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class TranscriptGenerationService : BaseService, ITranscriptGenerationService
{
    private readonly LmsDbContext _dbContext;

    public TranscriptGenerationService(LmsDbContext dbContext, IAuditService auditService) : base(auditService)
    {
        _dbContext = dbContext;
    }

    public async Task<ErrorOr<TranscriptDto>> GenerateTranscriptAsync(Guid studentId, bool isOfficial = true, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .Include(x => x.AcademicProgram)
            .Include(x => x.Level)
            .Include(x => x.AdmissionApplication)
            .ThenInclude(a => a!.AcademicSession)
            .Include(x => x.Faculty)
            .FirstOrDefaultAsync(x => x.Id == studentId, ct);

        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        var courseOfferingIds = await _dbContext.CourseEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.CourseOfferingId).Distinct().ToListAsync(ct);

        // Get all assessments for these course offerings
        var assessmentIds = await _dbContext.Assessments
            .Where(a => courseOfferingIds.Contains(a.CourseOfferingId))
            .Select(a => a.Id)
            .ToListAsync(ct);

        // Get all grades for this student
        var grades = await _dbContext.Grades
            .Where(g => g.StudentId == studentId && assessmentIds.Contains(g.AssessmentId))
            .Include(g => g.Assessment)
                .ThenInclude(a => a!.CourseOffering)
                    .ThenInclude(co => co!.Course)
            .Include(g => g.Assessment)
                .ThenInclude(a => a!.CourseOffering)
                    .ThenInclude(co => co!.AcademicSession)
            .ToListAsync(ct);

        // Build course records
        var courseRecords = new List<TranscriptCourseRecord>();
        foreach (var offeringId in courseOfferingIds.Distinct())
        {
            var offering = await _dbContext.CourseOfferings
                .Include(co => co.Course)
                .Include(co => co.AcademicSession)
                .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

            if (offering == null) continue;

            var studentGrades = grades.Where(g => g.Assessment!.CourseOfferingId == offeringId).ToList();
            var hasGrade = studentGrades.Any(g => g.MarksObtained > 0);

            if (!hasGrade && offering.Semester != 0) // Include all offerings or those with grades
            {
                // Check if there are any assessments for this offering
                var assessmentCount = await _dbContext.Assessments
                    .CountAsync(a => a.CourseOfferingId == offeringId, ct);
                if (assessmentCount == 0) continue;
            }

            var gradeRecord = studentGrades.FirstOrDefault(g => g.MarksObtained > 0);
            var attendancePercentage = await CalculateAttendancePercentage(offeringId, studentId, ct);

            courseRecords.Add(new TranscriptCourseRecord(
                offeringId,
                offering.Course?.Code ?? "N/A",
                offering.Course?.Title ?? "N/A",
                offering.Course?.CreditUnits ?? 0,
                (int)offering.Semester,
                offering.AcademicSession?.Name ?? "N/A",
                gradeRecord.MarksObtained > 0 ? CalculateLetterGrade(gradeRecord.MarksObtained) : null,
                gradeRecord.MarksObtained > 0 ? ConvertToGradePoints(gradeRecord.MarksObtained) : null,
                attendancePercentage));
        }

        // Calculate cumulative GPA
        var passedGrades = grades.Where(g => g.MarksObtained >= 40).ToList();
        var cumulativeGpa = passedGrades.Any() ? CalculateGpa(passedGrades) : 0;
        var totalCreditsEarned = await CalculateTotalCreditsEarned(grades, ct);

        // Get academic standing
        var standing = await _dbContext.AcademicStandings
            .Where(s => s.StudentId == studentId && (s.ExpiryDate == null || s.ExpiryDate > DateTime.UtcNow))
            .OrderByDescending(s => s.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        return new TranscriptDto(
            studentId,
            $"{student.FirstName} {student.LastName}",
            student.StudentNumber ?? "N/A",
            student.OfficialEmail,
            student.AcademicProgram?.Name ?? "N/A",
            student.Level?.Name ?? "N/A",
            student.AcademicProgram?.Type ?? Data.Enums.ProgramType.Undergraduate,
            student.AdmissionApplication?.DateOfBirth ?? DateTime.UtcNow.AddYears(-20),
            student.AdmissionApplication?.Nationality ?? "N/A",
            student.AdmissionApplication?.AcademicSession?.Name ?? "N/A",
            courseRecords.OrderBy(x => x.AcademicSessionName).ThenBy(x => x.Semester).ToList(),
            cumulativeGpa,
            totalCreditsEarned,
            standing?.StandingType.ToString() ?? "GoodStanding",
            isOfficial,
            "System",
            DateTime.UtcNow);
    }

    public async Task<ErrorOr<TranscriptRequestDto>> CreateTranscriptRequestAsync(Guid studentId, CreateTranscriptRequestDto request, Guid requestedBy, CancellationToken ct = default)
    {
        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == studentId, ct);
        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        var transcriptRequest = new TranscriptRequest
        {
            StudentId = studentId,
            Status = TranscriptStatus.Pending,
            IsOfficial = request.IsOfficial,
            DeliveryEmail = request.DeliveryEmail,
            DeliveryMethod = request.DeliveryMethod ?? "Email",
            Remarks = request.Remarks,
            FeeAmount = request.IsOfficial ? 5000m : 0m, // Default fee for official transcript
            FeePaid = false,
            CreatedById = requestedBy,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TranscriptRequests.Add(transcriptRequest);
        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("CreateTranscriptRequest", "TranscriptRequest", transcriptRequest.Id.ToString(),
            $"Created transcript request for student {studentId}", ct);

        return MapToTranscriptRequestDto(transcriptRequest);
    }

    public async Task<ErrorOr<TranscriptRequestDto>> GetTranscriptRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await _dbContext.TranscriptRequests
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (request == null)
            return DomainErrors.Reporting.TranscriptNotFound;

        return MapToTranscriptRequestDto(request);
    }

    public async Task<ErrorOr<List<TranscriptRequestDto>>> GetStudentTranscriptRequestsAsync(Guid studentId, CancellationToken ct = default)
    {
        var requests = await _dbContext.TranscriptRequests
            .Where(x => x.StudentId == studentId)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(MapToTranscriptRequestDto).ToList();
    }

    public async Task<ErrorOr<List<TranscriptRequestDto>>> GetAllTranscriptRequestsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var requests = await _dbContext.TranscriptRequests
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return requests.Select(MapToTranscriptRequestDto).ToList();
    }

    public async Task<ErrorOr<TranscriptRequestDto>> ProcessTranscriptRequestAsync(Guid requestId, Guid processedBy, CancellationToken ct = default)
    {
        var request = await _dbContext.TranscriptRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (request == null)
            return DomainErrors.Reporting.TranscriptNotFound;

        if (request.Status != TranscriptStatus.Pending && request.Status != TranscriptStatus.Processing)
            return Error.Conflict("Transcript.AlreadyProcessed", "Transcript request has already been processed");

        request.Status = TranscriptStatus.Ready;
        request.ProcessedBy = processedBy;
        request.UpdatedAt = DateTime.UtcNow;

        // Generate the transcript document URL (in production, this would generate an actual PDF)
        var transcript = await GenerateTranscriptAsync(request.StudentId, request.IsOfficial, ct);
        if (!transcript.IsError)
        {
            request.DocumentUrl = $"/transcripts/{request.Id}.pdf";
        }

        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("ProcessTranscriptRequest", "TranscriptRequest", request.Id.ToString(),
            $"Processed transcript request by {processedBy}", ct);

        return MapToTranscriptRequestDto(request);
    }

    private decimal CalculateGpa(IEnumerable<Grade> grades)
    {
        if (!grades.Any()) return 0;

        const decimal creditUnits = 3m;
        decimal totalPoints = 0;
        decimal totalCredits = 0;

        foreach (var grade in grades)
        {
            if (grade.MarksObtained < 40) continue;

            totalPoints += grade.MarksObtained * creditUnits;
            totalCredits += creditUnits;
        }

        return totalCredits > 0 ? Math.Round(totalPoints / totalCredits, 2) : 0;
    }

    private async Task<int> CalculateTotalCreditsEarned(IEnumerable<Grade> grades, CancellationToken ct)
    {
        var gradeList = grades.ToList();
        var assessmentIds = gradeList.Select(g => g.AssessmentId).ToList();

        var assessments = await _dbContext.Assessments
            .Include(a => a.CourseOffering)
                .ThenInclude(co => co!.Course)
            .Where(a => assessmentIds.Contains(a.Id))
            .ToListAsync(ct);

        int creditsEarned = 0;
        foreach (var grade in gradeList)
        {
            if (grade.MarksObtained < 40) continue;

            var assessment = assessments.FirstOrDefault(a => a.Id == grade.AssessmentId);
            if (assessment?.CourseOffering?.Course != null)
            {
                creditsEarned += assessment.CourseOffering.Course.CreditUnits;
            }
        }

        return creditsEarned;
    }

    private async Task<int> CalculateAttendancePercentage(Guid courseOfferingId, Guid studentId, CancellationToken ct)
    {
        var totalSessions = await _dbContext.LectureSessions
            .CountAsync(s => s.CourseOfferingId == courseOfferingId, ct);

        if (totalSessions == 0) return 0;

        var attendedSessions = await _dbContext.SessionAttendances
            .CountAsync(a => a.LectureSession.CourseOfferingId == courseOfferingId
                && a.StudentId == studentId
                && a.IsPresent, ct);

        return totalSessions > 0 ? (int)((decimal)attendedSessions / totalSessions * 100) : 0;
    }

    private string CalculateLetterGrade(decimal marks)
    {
        return marks switch
        {
            >= 70 => "A",
            >= 60 => "B",
            >= 50 => "C",
            >= 45 => "D",
            >= 40 => "E",
            _ => "F"
        };
    }

    private decimal ConvertToGradePoints(decimal marks)
    {
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

    private TranscriptRequestDto MapToTranscriptRequestDto(TranscriptRequest request)
    {
        return new TranscriptRequestDto(
            request.Id,
            request.StudentId,
            request.Student?.DisplayName ?? "Unknown",
            request.IsOfficial,
            request.Status,
            request.DeliveryEmail,
            request.DeliveryMethod ?? "Email",
            request.FeeAmount,
            request.FeePaid,
            request.DocumentUrl,
            !string.IsNullOrWhiteSpace(request.Processor?.DisplayName) ? request.Processor.DisplayName : null,
            request.CreatedAt,
            request.CompletedAt);
    }
}
