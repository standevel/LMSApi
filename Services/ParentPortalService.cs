using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using LMS.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class ParentPortalService : BaseService, IParentPortalService
{
    private readonly LmsDbContext _context;
    private readonly IGpaCalculationService _gpaService;
    private readonly IDegreeAuditService _degreeAuditService;

    public ParentPortalService(
        LmsDbContext context, 
        IAuditService auditService,
        IGpaCalculationService gpaService,
        IDegreeAuditService degreeAuditService) : base(auditService)
    {
        _context = context;
        _gpaService = gpaService;
        _degreeAuditService = degreeAuditService;
    }

    public async Task<ErrorOr<List<ParentStudentLinkDto>>> GetLinkedStudentsAsync(Guid parentId, CancellationToken ct = default)
    {
        var parentGuardian = await _context.ParentGuardians
            .FirstOrDefaultAsync(pg => pg.Id == parentId, ct);

        if (parentGuardian == null)
        {
            return Error.NotFound("ParentGuardian.NotFound", "Parent guardian not found.");
        }

        var links = await _context.ParentStudentLinks
            .Include(psl => psl.Student)
            .Where(psl => psl.ParentGuardianId == parentId)
            .ToListAsync(ct);

        return links.Select(psl => new ParentStudentLinkDto(
            psl.Id,
            psl.ParentGuardianId,
            psl.StudentId,
            psl.Student?.StudentNumber,
            psl.Student != null ? $"{psl.Student.FirstName} {psl.Student.LastName}".Trim() : "Unknown",
            psl.Student != null ? (psl.Student.OfficialEmail ?? string.Empty) : string.Empty,
            psl.Student != null ? psl.Student.Status == StudentStatus.Active : false,
            psl.LinkedAtUtc)).ToList();
    }

    private static string ComputeLetterGrade(decimal totalMarks, List<LMS.Api.Contracts.GradeMappingDto>? mappings = null)
    {
        if (mappings == null || !mappings.Any())
        {
            if (totalMarks >= 70) return "A";
            if (totalMarks >= 60) return "B";
            if (totalMarks >= 50) return "C";
            if (totalMarks >= 40) return "D";
            if (totalMarks >= 30) return "E";
            return "F";
        }
        
        var match = mappings.OrderByDescending(m => m.MinPercentage)
            .FirstOrDefault(m => totalMarks >= m.MinPercentage);
            
        return match?.LetterGrade ?? "F";
    }

    public async Task<ErrorOr<StudentProgressDto>> GetStudentProgressAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var student = await _context.Students
            .Include(s => s.AcademicProgram)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
        {
            return Error.NotFound("Student.NotFound", "Student not found.");
        }

        decimal gpa = 0.0m;
        int creditsEarned = 0;
        int creditsRequired = student.AcademicProgram != null ? student.AcademicProgram.DurationYears * 40 : 120;

        var gpaResult = await _gpaService.GetStudentGpaAsync(studentId, null, ct);
        if (!gpaResult.IsError)
        {
            gpa = gpaResult.Value.CumulativeGpa;
            creditsEarned = gpaResult.Value.TotalCreditsEarned;
        }

        var auditsResult = await _degreeAuditService.GetStudentDegreeAuditsAsync(studentId, ct);
        if (!auditsResult.IsError && auditsResult.Value.Any())
        {
            var audit = auditsResult.Value.First();
            creditsRequired = audit.TotalCreditsRequired;
            if (audit.TotalCreditsEarned > 0)
            {
                creditsEarned = audit.TotalCreditsEarned;
            }
        }

        var sysConfig = await _context.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        var mappings = string.IsNullOrEmpty(sysConfig?.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
            ? new List<LMS.Api.Contracts.GradeMappingDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<LMS.Api.Contracts.GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
              ?? new List<LMS.Api.Contracts.GradeMappingDto>();

        var rStrategy = sysConfig?.RoundingStrategy ?? RoundingStrategy.Standard;
        var decimalPlaces = sysConfig?.RoundingDecimalPlaces ?? 0;
        var graceThreshold = sysConfig?.GraceThreshold ?? 0.0m;

        var enrollmentsQuery = _context.CourseEnrollments
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .Where(e => e.StudentId == studentId && e.Status == "Registered");

        if (academicSessionId.HasValue)
            enrollmentsQuery = enrollmentsQuery.Where(e => e.CourseOffering.AcademicSessionId == academicSessionId.Value);

        var enrollments = await enrollmentsQuery.ToListAsync(ct);

        var courseProgress = new List<CourseProgressDto>();

        foreach (var enrollment in enrollments)
        {
            var offering = enrollment.CourseOffering;
            if (offering == null) continue;

            var totalMarks = await _context.Grades
                .Where(g => g.Assessment!.CourseOfferingId == offering.Id && g.StudentId == studentId)
                .SumAsync(g => g.MarksObtained, ct);

            if (totalMarks > 100m) totalMarks = 100m;
            var gradeResult = GradeCalculator.CalculateGrade(totalMarks, rStrategy, decimalPlaces, graceThreshold, mappings);
            var currentGrade = gradeResult.LetterGrade;
            bool isCompleted = gradeResult.Score >= 40m;

            var totalSessions = await _context.LectureSessions
                .CountAsync(ls => ls.CourseOfferingId == offering.Id && ls.IsCompleted, ct);

            var attendedSessions = await _context.SessionAttendances
                .CountAsync(sa => sa.LectureSession.CourseOfferingId == offering.Id && sa.StudentId == studentId && sa.IsPresent, ct);

            var attendancePercentage = totalSessions > 0
                ? (int)Math.Round((double)attendedSessions / totalSessions * 100)
                : 100;

            courseProgress.Add(new CourseProgressDto(
                offering.Id,
                offering.Course?.Code ?? string.Empty,
                offering.Course?.Title ?? string.Empty,
                attendancePercentage,
                currentGrade,
                isCompleted,
                offering.AcademicSession?.Name,
                offering.AcademicSessionId));
        }

        return new StudentProgressDto(
            student.Id,
            $"{student.FirstName} {student.LastName}".Trim(),
            student.StudentNumber ?? string.Empty,
            gpa,
            creditsEarned,
            creditsRequired,
            courseProgress);
    }

    public async Task<ErrorOr<StudentGradesDto>> GetStudentGradesAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
        {
            return Error.NotFound("Student.NotFound", "Student not found.");
        }

        var sysConfig = await _context.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
            
        var mappings = string.IsNullOrEmpty(sysConfig?.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
            ? new List<LMS.Api.Contracts.GradeMappingDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<LMS.Api.Contracts.GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
              ?? new List<LMS.Api.Contracts.GradeMappingDto>();

        var rStrategy = sysConfig?.RoundingStrategy ?? RoundingStrategy.Standard;
        var decimalPlaces = sysConfig?.RoundingDecimalPlaces ?? 0;
        var graceThreshold = sysConfig?.GraceThreshold ?? 0.0m;

        var enrollmentsQuery = _context.CourseEnrollments
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .Where(e => e.StudentId == studentId && e.Status == "Registered");

        if (academicSessionId.HasValue)
            enrollmentsQuery = enrollmentsQuery.Where(e => e.CourseOffering.AcademicSessionId == academicSessionId.Value);

        var enrollments = await enrollmentsQuery.ToListAsync(ct);

        var grades = new List<StudentGradeDto>();

        foreach (var enrollment in enrollments)
        {
            var offering = enrollment.CourseOffering;
            if (offering == null) continue;

            var totalMarks = await _context.Grades
                .Where(g => g.Assessment!.CourseOfferingId == offering.Id && g.StudentId == studentId)
                .SumAsync(g => g.MarksObtained, ct);

            if (totalMarks > 100m) totalMarks = 100m;
            var gradeResult = GradeCalculator.CalculateGrade(totalMarks, rStrategy, decimalPlaces, graceThreshold, mappings);
            var gradeLetter = gradeResult.LetterGrade;

            grades.Add(new StudentGradeDto(
                offering.Id,
                offering.Course?.Code ?? string.Empty,
                offering.Course?.Title ?? string.Empty,
                gradeLetter,
                offering.AcademicSession?.Name,
                offering.AcademicSessionId));
        }

        var fullName = !string.IsNullOrWhiteSpace(student.FirstName) || !string.IsNullOrWhiteSpace(student.LastName) 
            ? $"{student.FirstName} {student.LastName}".Trim() 
            : "Unknown";

        return new StudentGradesDto(
            student.Id,
            fullName,
            student.StudentNumber ?? string.Empty,
            grades);
    }

    public async Task<ErrorOr<bool>> SendMessageToStudentAsync(Guid studentId, Guid parentUserId, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Error.Validation("InvalidInput", "Message content is required.");
        }

        var student = await _context.Students.FindAsync(new object[] { studentId }, ct);
        if (student == null)
        {
            return Error.NotFound("Student.NotFound", "Student not found.");
        }

        var parent = await _context.ParentGuardians
            .FirstOrDefaultAsync(pg => pg.UserId == parentUserId, ct);
        if (parent == null)
        {
            return Error.NotFound("ParentGuardian.NotFound", "Parent guardian not found.");
        }

        // Verify that the parent is actually linked to this student
        var link = await _context.ParentStudentLinks
            .FirstOrDefaultAsync(psl => psl.ParentGuardianId == parent.Id && psl.StudentId == studentId, ct);

        if (link == null)
        {
            return Error.Forbidden("AccessDenied", "Parent is not linked to this student.");
        }

        var recipient = await _context.Users.FirstOrDefaultAsync(user =>
            (!string.IsNullOrWhiteSpace(student.EntraObjectId) && user.EntraObjectId == student.EntraObjectId)
            || user.EntraObjectId == $"student:{student.Id}"
            || (!string.IsNullOrWhiteSpace(student.OfficialEmail) && user.Email == student.OfficialEmail), ct);

        if (recipient == null)
        {
            return Error.NotFound("Student.AccountNotFound", "The linked student does not have an active user account.");
        }

        var message = new Message
        {
            SenderId = parentUserId,
            RecipientId = recipient.Id,
            Content = content.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false,
            IsActive = true
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("SendMessageToStudent", "ParentMessage", message.Id.ToString(),
            $"Parent {parent.Id} sent message to student {studentId}", ct);

        return true;
    }
}
