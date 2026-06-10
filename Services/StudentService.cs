using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public interface IStudentService
{
    Task<(IEnumerable<StudentSummaryDto> Students, int TotalCount)> GetStudentsAsync(
        string? search, string? programId, string? sessionId, string? status,
        int page, int pageSize, CancellationToken ct);

    Task<StudentDetailDto?> GetStudentDetailAsync(Guid studentId, CancellationToken ct);

    Task<IEnumerable<StudentAssignmentDto>> GetStudentAssignmentsAsync(Guid studentId, CancellationToken ct);

    Task<IEnumerable<StudentCourseResultDto>> GetStudentResultsAsync(Guid studentId, CancellationToken ct);

    Task<(IEnumerable<StudentFeeRecordDto> Records, IEnumerable<StudentFeePaymentDto> Payments)> GetStudentFeesAsync(Guid studentId, CancellationToken ct);

    Task<IEnumerable<StudentParentDto>> GetStudentParentsAsync(Guid studentId, CancellationToken ct);

    Task<IEnumerable<StudentEnrollmentDto>> GetStudentEnrollmentsAsync(Guid studentId, CancellationToken ct);
}

public class StudentService(LmsDbContext context) : IStudentService
{
    public async Task<(IEnumerable<StudentSummaryDto> Students, int TotalCount)> GetStudentsAsync(
        string? search, string? programId, string? sessionId, string? status,
        int page, int pageSize, CancellationToken ct)
    {
        var query = context.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Faculty)
            .Include(s => s.Level)
            .Include(s => s.AcademicSession)
            .AsQueryable();

        // Filter by search (name or matric number)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(lower) ||
                s.LastName.ToLower().Contains(lower) ||
                s.MiddleName != null && s.MiddleName.ToLower().Contains(lower) ||
                s.StudentNumber != null && s.StudentNumber.ToLower().Contains(lower) ||
                s.PersonalEmail.ToLower().Contains(lower));
        }

        // Filter by program
        if (!string.IsNullOrWhiteSpace(programId) && Guid.TryParse(programId, out var progId))
        {
            query = query.Where(s => s.AcademicProgramId == progId);
        }

        // Filter by session
        if (!string.IsNullOrWhiteSpace(sessionId) && Guid.TryParse(sessionId, out var sessId))
        {
            query = query.Where(s => s.AcademicSessionId == sessId);
        }

        // Filter by status
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<StudentStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(s => s.Status == parsedStatus);
            }
        }

        var totalCount = await query.CountAsync(ct);

        var students = await query
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentSummaryDto
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName,
                MiddleName = s.MiddleName,
                StudentNumber = s.StudentNumber,
                PersonalEmail = s.PersonalEmail,
                OfficialEmail = s.OfficialEmail,
                Phone = s.Phone,
                ProgramName = s.AcademicProgram != null ? s.AcademicProgram.Name : null,
                FacultyName = s.Faculty != null ? s.Faculty.Name : null,
                LevelName = s.Level != null ? s.Level.Name : null,
                SessionName = s.AcademicSession != null ? s.AcademicSession.Name : null,
                Status = s.Status.ToString(),
                EnrollmentDate = s.EnrollmentDate,
                GraduationDate = s.GraduationDate,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync(ct);

        return (students, totalCount);
    }

    public async Task<StudentDetailDto?> GetStudentDetailAsync(Guid studentId, CancellationToken ct)
    {
        var student = await context.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Faculty)
            .Include(s => s.Level)
            .Include(s => s.AcademicSession)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null) return null;

        return new StudentDetailDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            MiddleName = student.MiddleName,
            StudentNumber = student.StudentNumber,
            PersonalEmail = student.PersonalEmail,
            OfficialEmail = student.OfficialEmail,
            Phone = student.Phone,
            EmergencyContactName = student.EmergencyContactName,
            EmergencyContactPhone = student.EmergencyContactPhone,
            EmergencyContactEmail = student.EmergencyContactEmail,
            ProgramName = student.AcademicProgram?.Name,
            FacultyName = student.Faculty?.Name,
            LevelName = student.Level?.Name,
            SessionName = student.AcademicSession?.Name,
            Status = student.Status.ToString(),
            EnrollmentDate = student.EnrollmentDate,
            GraduationDate = student.GraduationDate,
            CreatedAt = student.CreatedAt,
            UpdatedAt = student.UpdatedAt,
            JambRegistrationNumber = student.JambRegistrationNumber,
            JambScore = student.JambScore,
            AdmissionApplicationId = student.AdmissionApplicationId?.ToString()
        };
    }

    public async Task<IEnumerable<StudentAssignmentDto>> GetStudentAssignmentsAsync(Guid studentId, CancellationToken ct)
    {
        var grades = await context.Grades
            .Include(g => g.Assessment)
                .ThenInclude(a => a!.AssessmentCategory)
            .Include(g => g.Assessment)
                .ThenInclude(a => a!.CourseOffering)
                    .ThenInclude(c => c!.Course)
            .Where(g => g.StudentId == studentId)
            .ToListAsync(ct);

        return grades.Select(g => new StudentAssignmentDto
        {
            Id = g.Id,
            CourseCode = g.Assessment?.CourseOffering?.Course?.Code ?? string.Empty,
            CourseTitle = g.Assessment?.CourseOffering?.Course?.Title ?? string.Empty,
            AssessmentTitle = g.Assessment?.Title ?? string.Empty,
            Category = g.Assessment?.AssessmentCategory?.CategoryName ?? string.Empty,
            MaxMarks = g.Assessment?.MaxMarks ?? 0,
            MarksObtained = g.MarksObtained,
            Grade = null, // Will be computed by GradebookService if needed
            Remarks = g.Remarks,
            AssessmentDate = g.Assessment?.AssessmentDate,
            DueDate = g.Assessment?.DueDate,
            IsLocked = g.IsLocked
        });
    }

    public async Task<IEnumerable<StudentCourseResultDto>> GetStudentResultsAsync(Guid studentId, CancellationToken ct)
    {
        var courseOfferings = await context.CourseOfferings
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .ToListAsync(ct);

        var results = new List<StudentCourseResultDto>();

        foreach (var offering in courseOfferings)
        {
            // Calculate total CA (sum of all assessment marks for this student)
            var totalCA = await context.Grades
                .Where(g => g.Assessment!.CourseOfferingId == offering.Id && g.StudentId == studentId)
                .SumAsync(g => (double?)g.MarksObtained ?? 0.0, ct);

            // For simplicity, treat CA as a portion and exam as remaining
            // In a real system, CA categories and exam categories would be separate
            var maxCaMarks = 40m;
            var maxExamMarks = 60m;

            var caScore = totalCA > (double)maxCaMarks ? maxCaMarks : (decimal)totalCA;
            var examScore = 0m; // Would need separate exam data

            var totalMarks = caScore + examScore;

            // Compute grade and point
            var (grade, point) = ComputeGrade(totalMarks);

            results.Add(new StudentCourseResultDto
            {
                CourseOfferingId = offering.Id,
                CourseCode = offering.Course?.Code ?? string.Empty,
                CourseTitle = offering.Course?.Title ?? string.Empty,
                CreditUnits = offering.Course?.CreditUnits ?? 0,
                Semester = offering.Semester.ToString(),
                Level = offering.LevelId != Guid.Empty ? 0 : 0, // Would need level lookup
                TotalCA = (decimal)caScore,
                TotalExam = examScore,
                TotalMarks = totalMarks,
                Grade = grade,
                Point = point,
                IsPublished = false
            });
        }

        return results;
    }

    public async Task<(IEnumerable<StudentFeeRecordDto> Records, IEnumerable<StudentFeePaymentDto> Payments)> GetStudentFeesAsync(Guid studentId, CancellationToken ct)
    {
        var records = await context.StudentFeeRecords
            .Include(r => r.Student)
            .Include(r => r.Session)
            .Where(r => r.StudentId == studentId)
            .ToListAsync(ct);

        var payments = await context.FeePayments
            .Where(p => p.StudentFeeRecord.StudentId == studentId)
            .ToListAsync(ct);

        var mappedRecords = records.Select(r => new StudentFeeRecordDto
            {
                Id = r.Id,
                SessionName = r.Session?.Name ?? string.Empty,
                TotalAmount = r.TotalAmount,
                AmountPaid = r.AmountPaid,
                Balance = r.Balance,
                Status = r.Status.ToString(),
                GeneratedAt = r.GeneratedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

        var mappedPayments = payments.Select(p => new StudentFeePaymentDto
            {
                Id = p.Id,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod.ToString(),
                ReferenceNumber = p.ReferenceNumber,
                ReceiptUrl = p.ReceiptUrl,
                GatewayReference = p.GatewayReference,
                Status = p.Status.ToString(),
                RejectionReason = p.RejectionReason,
                PaidAt = p.PaidAt,
                ConfirmedAt = p.ConfirmedAt,
                ConfirmedBy = p.ConfirmedBy
            }).ToList();

        return (mappedRecords, mappedPayments);
    }

    public async Task<IEnumerable<StudentParentDto>> GetStudentParentsAsync(Guid studentId, CancellationToken ct)
    {
        var links = await context.ParentStudentLinks
            .Include(l => l.ParentGuardian)
            .Where(l => l.StudentId == studentId)
            .ToListAsync(ct);

        return links.Select(l => new StudentParentDto
        {
            Id = l.ParentGuardianId,
            FirstName = l.ParentGuardian?.FirstName ?? string.Empty,
            LastName = l.ParentGuardian?.LastName ?? string.Empty,
            PhoneNumber = l.ParentGuardian?.PhoneNumber ?? string.Empty,
            Email = l.ParentGuardian?.Email ?? string.Empty,
            DateAdded = l.LinkedAtUtc
        });
    }

    public async Task<IEnumerable<StudentEnrollmentDto>> GetStudentEnrollmentsAsync(Guid studentId, CancellationToken ct)
    {
        var enrollments = await context.Enrollments
            .Include(e => e.Program)
            .Include(e => e.Level)
            .Include(e => e.AcademicSession)
            .Where(e => e.UserId == studentId)
            .ToListAsync(ct);

        return enrollments.Select(e => new StudentEnrollmentDto
        {
            Id = e.Id,
            ProgramName = e.Program?.Name ?? string.Empty,
            LevelName = e.Level?.Name ?? string.Empty,
            SessionName = e.AcademicSession?.Name ?? string.Empty,
            EnrolledAt = e.EnrolledAtUtc
        });
    }

    private static (string Grade, string Point) ComputeGrade(decimal totalMarks)
    {
        if (totalMarks >= 70) return ("A", "5.00");
        if (totalMarks >= 60) return ("B", "4.00");
        if (totalMarks >= 50) return ("C", "3.00");
        if (totalMarks >= 40) return ("D", "2.00");
        if (totalMarks >= 30) return ("E", "1.00");
        return ("F", "0.00");
    }
}
