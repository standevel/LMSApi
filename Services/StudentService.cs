using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public interface IStudentService
{
    Task<(IEnumerable<StudentSummaryDto> Students, int TotalCount)> GetStudentsAsync(
        string? search, string? programId, string? departmentId, string? facultyId, string? levelId, string? sessionId, string? status,
        string? sortBy, string? sortDirection, int page, int pageSize, CancellationToken ct);

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
        string? search, string? programId, string? departmentId, string? facultyId, string? levelId, string? sessionId, string? status,
        string? sortBy, string? sortDirection, int page, int pageSize, CancellationToken ct)
    {
        var query = context.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Faculty)
            .Include(s => s.Level)
            .Include(s => s.AcademicSession)
            .AsQueryable();

        // Filter by search (name or matric number or emails or IDs)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(lower) ||
                s.LastName.ToLower().Contains(lower) ||
                s.MiddleName != null && s.MiddleName.ToLower().Contains(lower) ||
                s.StudentNumber != null && s.StudentNumber.ToLower().Contains(lower) ||
                s.PersonalEmail.ToLower().Contains(lower) ||
                s.OfficialEmail.ToLower().Contains(lower) ||
                s.EntraObjectId.ToLower().Contains(lower) ||
                s.JambRegistrationNumber != null && s.JambRegistrationNumber.ToLower().Contains(lower));
        }

        // Filter by program
        if (!string.IsNullOrWhiteSpace(programId) && Guid.TryParse(programId, out var progId))
        {
            query = query.Where(s => s.AcademicProgramId == progId);
        }

        // Filter by department
        if (!string.IsNullOrWhiteSpace(departmentId) && Guid.TryParse(departmentId, out var deptId))
        {
            query = query.Where(s => s.AcademicProgram != null && s.AcademicProgram.DepartmentId == deptId);
        }

        // Filter by faculty
        if (!string.IsNullOrWhiteSpace(facultyId) && Guid.TryParse(facultyId, out var facId))
        {
            query = query.Where(s => s.FacultyId == facId || (s.AcademicProgram != null && s.AcademicProgram.Department != null && s.AcademicProgram.Department.FacultyId == facId));
        }

        // Filter by level
        if (!string.IsNullOrWhiteSpace(levelId))
        {
            if (Guid.TryParse(levelId, out var levId))
            {
                query = query.Where(s => s.LevelId == levId);
            }
            else
            {
                var searchLevel = levelId.Trim();
                query = query.Where(s => s.Level != null && s.Level.Name == searchLevel);
            }
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

        if (!string.IsNullOrWhiteSpace(sortBy) && sortBy.Equals("level", StringComparison.OrdinalIgnoreCase))
        {
            query = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(s => s.Level != null ? s.Level.Name : "").ThenBy(s => s.LastName).ThenBy(s => s.FirstName)
                : query.OrderBy(s => s.Level != null ? s.Level.Name : "").ThenBy(s => s.LastName).ThenBy(s => s.FirstName);
        }
        else
        {
            query = query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName);
        }

        var students = await query
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
                DepartmentName = s.AcademicProgram != null && s.AcademicProgram.Department != null ? s.AcademicProgram.Department.Name : null,
                FacultyName = s.Faculty != null ? s.Faculty.Name : null,
                LevelName = s.Level != null ? s.Level.Name : null,
                SessionName = s.AcademicSession != null ? s.AcademicSession.Name : null,
                Status = s.Status.ToString(),
                EnrollmentDate = s.EnrollmentDate,
                GraduationDate = s.GraduationDate,
                UpdatedAt = s.UpdatedAt,
                JambRegistrationNumber = s.JambRegistrationNumber
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
        var sysConfig = await context.SystemGradingConfigurations
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

        var studentEntity = await context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (studentEntity == null)
            return Enumerable.Empty<StudentCourseResultDto>();

        var appUser = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == studentEntity.OfficialEmail, ct);

        if (appUser == null)
            return Enumerable.Empty<StudentCourseResultDto>();

        var userId = appUser.Id;

        var enrolledOfferingIds = await context.CourseEnrollments
            .Where(e => e.StudentId == userId && e.Status != "Dropped")
            .Select(e => e.CourseOfferingId)
            .ToListAsync(ct);

        var gradedOfferingIds = await context.Grades
            .Where(g => g.StudentId == userId)
            .Select(g => g.Assessment.CourseOfferingId)
            .Distinct()
            .ToListAsync(ct);

        var relevantOfferingIds = enrolledOfferingIds.Union(gradedOfferingIds).ToHashSet();

        if (!relevantOfferingIds.Any())
            return Enumerable.Empty<StudentCourseResultDto>();

        var courseOfferings = await context.CourseOfferings
            .Where(co => relevantOfferingIds.Contains(co.Id))
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .ToListAsync(ct);

        var publicationRaw = await context.GradePublications
            .Where(x => relevantOfferingIds.Contains(x.CourseOfferingId))
            .ToListAsync(ct);

        // Deduplicate by CourseOfferingId (oldest record wins) to handle any legacy duplicate rows
        var publications = publicationRaw
            .GroupBy(x => x.CourseOfferingId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedAt).First().IsVisibleToStudents);

        var results = new List<StudentCourseResultDto>();

        foreach (var offering in courseOfferings)
        {
            var categories = await context.AssessmentCategories
                .Where(x => x.CourseOfferingId == offering.Id)
                .ToListAsync(ct);

            var assessments = await context.Assessments
                .Where(x => x.CourseOfferingId == offering.Id)
                .Include(x => x.Grades)
                .ToListAsync(ct);

            decimal CalculateCategoryScoreHelper(AssessmentCategoryType categoryType)
            {
                var category = categories.FirstOrDefault(c => c.CategoryType == categoryType);
                if (category == null) return 0m;

                var categoryAssessments = assessments.Where(a => a.AssessmentCategoryId == category.Id).ToList();
                if (!categoryAssessments.Any()) return 0m;

                var totalObtained = 0m;
                var totalMaxMarks = 0m;

                foreach (var assessment in categoryAssessments)
                {
                    var grade = assessment.Grades.FirstOrDefault(g => g.StudentId == userId);
                    totalObtained += grade?.MarksObtained ?? 0m;
                    totalMaxMarks += assessment.MaxMarks;
                }

                if (totalMaxMarks == 0) return 0m;
                return totalObtained / totalMaxMarks * 100m; // Return percentage
            }

            var ca1Score = CalculateCategoryScoreHelper(AssessmentCategoryType.CA1);
            var ca2Score = CalculateCategoryScoreHelper(AssessmentCategoryType.CA2);
            var ca3Score = CalculateCategoryScoreHelper(AssessmentCategoryType.CA3);
            var examScoreRaw = CalculateCategoryScoreHelper(AssessmentCategoryType.Exam);

            var gradingStyle = sysConfig?.DefaultGradingStyle ?? GradingStyle.Weighted;
            decimal totalScore = 0m;

            decimal ca1Weight = sysConfig?.DefaultCA1Weight ?? 15m;
            decimal ca2Weight = sysConfig?.DefaultCA2Weight ?? 15m;
            decimal ca3Weight = sysConfig?.DefaultCA3Weight ?? 10m;
            decimal examWeight = sysConfig?.DefaultExamWeight ?? 60m;

            decimal ca1Contrib = ca1Score * ca1Weight / 100m;
            decimal ca2Contrib = ca2Score * ca2Weight / 100m;
            decimal ca3Contrib = ca3Score * ca3Weight / 100m;
            decimal examContrib = examScoreRaw * examWeight / 100m;

            if (gradingStyle == GradingStyle.Weighted)
            {
                totalScore = ca1Contrib + ca2Contrib + ca3Contrib + examContrib;
            }
            else
            {
                var scores = new[] { ca1Score, ca2Score, ca3Score, examScoreRaw }.Where(s => s >= 0).ToList();
                totalScore = scores.Any() ? scores.Average() : 0m;
            }

            decimal totalCA = ca1Contrib + ca2Contrib + ca3Contrib;
            decimal totalExam = examContrib;

            var gradeResult = GradeCalculator.CalculateGrade(totalScore, rStrategy, decimalPlaces, graceThreshold, mappings);

            results.Add(new StudentCourseResultDto
            {
                CourseOfferingId = offering.Id,
                CourseCode = offering.Course?.Code ?? string.Empty,
                CourseTitle = offering.Course?.Title ?? string.Empty,
                CreditUnits = offering.Course?.CreditUnits ?? 0,
                Semester = offering.Semester.ToString(),
                Level = 0,
                TotalCA = totalCA,
                TotalExam = totalExam,
                TotalMarks = gradeResult.Score,
                Grade = gradeResult.LetterGrade,
                Point = gradeResult.GradePoints.ToString("F2"),
                IsPublished = publications.TryGetValue(offering.Id, out var visible) && visible
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

    private static (string Grade, string Point) ComputeGrade(decimal totalMarks, List<LMS.Api.Contracts.GradeMappingDto>? mappings = null)
    {
        if (mappings == null || !mappings.Any())
        {
            if (totalMarks >= 70) return ("A", "5.00");
            if (totalMarks >= 60) return ("B", "4.00");
            if (totalMarks >= 50) return ("C", "3.00");
            if (totalMarks >= 40) return ("D", "2.00");
            if (totalMarks >= 30) return ("E", "1.00");
            return ("F", "0.00");
        }
        
        var match = mappings.OrderByDescending(m => m.MinPercentage)
            .FirstOrDefault(m => totalMarks >= m.MinPercentage);
            
        return (match?.LetterGrade ?? "F", match?.GradePoints.ToString("F2") ?? "0.00");
    }
}
