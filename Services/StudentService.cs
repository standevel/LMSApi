using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
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

    Task<ChangeStudentLevelItemResult> ChangeStudentLevelAsync(Guid studentId, ChangeStudentLevelRequest request, Guid? changedByUserId, CancellationToken ct);

    Task<BatchChangeStudentLevelResponse> BatchChangeStudentLevelAsync(BatchChangeStudentLevelRequest request, Guid? changedByUserId, CancellationToken ct);

    Task<ChangeStudentProgramItemResult> ChangeStudentProgramAsync(Guid studentId, ChangeStudentProgramRequest request, Guid? changedByUserId, CancellationToken ct);

    Task<BatchChangeStudentProgramResponse> BatchChangeStudentProgramAsync(BatchChangeStudentProgramRequest request, Guid? changedByUserId, CancellationToken ct);
}

public class StudentService(LmsDbContext context, IDegreeAuditService? degreeAuditService = null) : IStudentService
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
                ProgramId = s.AcademicProgramId,
                ProgramName = s.AcademicProgram != null ? s.AcademicProgram.Name : null,
                DepartmentName = s.AcademicProgram != null && s.AcademicProgram.Department != null ? s.AcademicProgram.Department.Name : null,
                FacultyName = s.Faculty != null ? s.Faculty.Name : null,
                LevelId = s.LevelId,
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
            ProgramId = student.AcademicProgramId,
            ProgramName = student.AcademicProgram?.Name,
            FacultyId = student.FacultyId,
            FacultyName = student.Faculty?.Name,
            LevelId = student.LevelId,
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

        var publishedSavedResults = await context.StudentCourseResults
            .Where(r => r.StudentId == userId && relevantOfferingIds.Contains(r.CourseOfferingId))
            .ToListAsync(ct);

        var results = new List<StudentCourseResultDto>();

        foreach (var offering in courseOfferings)
        {
            var saved = publishedSavedResults.FirstOrDefault(r => r.CourseOfferingId == offering.Id);
            if (saved != null)
            {
                decimal savedCA = (saved.Ca1Score ?? 0m) + (saved.Ca2Score ?? 0m) + (saved.Ca3Score ?? 0m);
                results.Add(new StudentCourseResultDto
                {
                    CourseOfferingId = offering.Id,
                    CourseCode = offering.Course?.Code ?? string.Empty,
                    CourseTitle = offering.Course?.Title ?? string.Empty,
                    CreditUnits = offering.Course?.CreditUnits ?? saved.CreditUnits,
                    Semester = offering.Semester.ToString(),
                    Level = 0,
                    TotalCA = savedCA,
                    TotalExam = saved.ExamScore ?? 0m,
                    TotalMarks = saved.TotalScore,
                    Grade = saved.LetterGrade,
                    Point = saved.GradePoints.ToString("F2"),
                    IsPublished = saved.IsPublished
                });
                continue;
            }

            var categories = await context.AssessmentCategories
                .Where(x => x.CourseOfferingId == offering.Id)
                .ToListAsync(ct);

            var assessments = await context.Assessments
                .Where(x => x.CourseOfferingId == offering.Id)
                .Include(x => x.Grades)
                .ToListAsync(ct);

            (decimal Obtained, decimal Percentage) CalculateCategoryScoreHelper(AssessmentCategoryType categoryType)
            {
                var category = categories.FirstOrDefault(c => c.CategoryType == categoryType);
                if (category == null) return (0m, 0m);

                var categoryAssessments = assessments.Where(a => a.AssessmentCategoryId == category.Id).ToList();
                if (!categoryAssessments.Any()) return (0m, 0m);

                var totalObtained = 0m;
                var totalMaxMarks = 0m;

                foreach (var assessment in categoryAssessments)
                {
                    var grade = assessment.Grades.FirstOrDefault(g => g.StudentId == userId);
                    totalObtained += grade?.MarksObtained ?? 0m;
                    totalMaxMarks += assessment.MaxMarks;
                }

                decimal percentage = totalMaxMarks == 0 ? 0m : totalObtained / totalMaxMarks * 100m;
                return (totalObtained, percentage);
            }

            var ca1 = CalculateCategoryScoreHelper(AssessmentCategoryType.CA1);
            var ca2 = CalculateCategoryScoreHelper(AssessmentCategoryType.CA2);
            var ca3 = CalculateCategoryScoreHelper(AssessmentCategoryType.CA3);
            var exam = CalculateCategoryScoreHelper(AssessmentCategoryType.Exam);

            var gradingStyle = sysConfig?.DefaultGradingStyle ?? GradingStyle.Weighted;
            decimal totalScore = 0m;
            decimal totalCA = 0m;
            decimal totalExam = 0m;

            if (gradingStyle == GradingStyle.Weighted)
            {
                decimal ca1Weight = sysConfig?.DefaultCA1Weight ?? 10m;
                decimal ca2Weight = sysConfig?.DefaultCA2Weight ?? 10m;
                decimal ca3Weight = sysConfig?.DefaultCA3Weight ?? 10m;
                decimal examWeight = sysConfig?.DefaultExamWeight ?? 70m;

                decimal ca1Contrib = ca1.Percentage * ca1Weight / 100m;
                decimal ca2Contrib = ca2.Percentage * ca2Weight / 100m;
                decimal ca3Contrib = ca3.Percentage * ca3Weight / 100m;
                decimal examContrib = exam.Percentage * examWeight / 100m;

                totalScore = ca1Contrib + ca2Contrib + ca3Contrib + examContrib;
                totalCA = ca1Contrib + ca2Contrib + ca3Contrib;
                totalExam = examContrib;
            }
            else
            {
                totalCA = ca1.Obtained + ca2.Obtained + ca3.Obtained;
                totalExam = exam.Obtained;
                totalScore = totalCA + totalExam;
            }

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

    public async Task<ChangeStudentLevelItemResult> ChangeStudentLevelAsync(
        Guid studentId, ChangeStudentLevelRequest request, Guid? changedByUserId, CancellationToken ct)
    {
        var batchReq = new BatchChangeStudentLevelRequest(
            [studentId],
            request.TargetLevelId,
            request.TargetLevelName,
            request.UpdateCurrentEnrollment);

        var batchRes = await BatchChangeStudentLevelAsync(batchReq, changedByUserId, ct);
        return batchRes.Results.FirstOrDefault() 
            ?? new ChangeStudentLevelItemResult(studentId, "Unknown", null, null, null, false, "Student not found or update failed.");
    }

    public async Task<BatchChangeStudentLevelResponse> BatchChangeStudentLevelAsync(
        BatchChangeStudentLevelRequest request, Guid? changedByUserId, CancellationToken ct)
    {
        if (request.StudentIds == null || !request.StudentIds.Any())
        {
            return new BatchChangeStudentLevelResponse(0, 0, 0, []);
        }

        if (!request.TargetLevelId.HasValue && string.IsNullOrWhiteSpace(request.TargetLevelName))
        {
            var failedResults = request.StudentIds.Select(id => new ChangeStudentLevelItemResult(
                id, "Student", null, null, null, false, "Neither target level ID nor target level name was provided.")).ToList();
            return new BatchChangeStudentLevelResponse(request.StudentIds.Count, 0, request.StudentIds.Count, failedResults);
        }

        AcademicLevel? explicitTargetLevel = null;
        if (request.TargetLevelId.HasValue)
        {
            explicitTargetLevel = await context.Levels
                .Include(l => l.Program)
                .FirstOrDefaultAsync(l => l.Id == request.TargetLevelId.Value, ct);

            if (explicitTargetLevel == null)
            {
                var failedResults = request.StudentIds.Select(id => new ChangeStudentLevelItemResult(
                    id, "Student", null, null, null, false, $"Target level with ID '{request.TargetLevelId.Value}' was not found.")).ToList();
                return new BatchChangeStudentLevelResponse(request.StudentIds.Count, 0, request.StudentIds.Count, failedResults);
            }
        }

        var targetName = (request.TargetLevelName ?? explicitTargetLevel?.Name ?? string.Empty).Trim();
        var targetOrder = explicitTargetLevel?.Order ?? 0;

        var students = await context.Students
            .Include(s => s.Level)
            .Include(s => s.AcademicProgram)
            .Where(s => request.StudentIds.Contains(s.Id))
            .ToListAsync(ct);

        var programIds = students
            .Where(s => s.AcademicProgramId.HasValue)
            .Select(s => s.AcademicProgramId!.Value)
            .Distinct()
            .ToList();

        var programLevels = await context.Levels
            .Where(l => programIds.Contains(l.ProgramId))
            .ToListAsync(ct);

        AcademicSession? activeSession = null;
        List<ProgramEnrollment> existingEnrollments = [];
        List<Curriculum> activeCurricula = [];

        if (request.UpdateCurrentEnrollment)
        {
            activeSession = await context.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive, ct);
            var studentIds = students.Select(s => s.Id).ToList();
            existingEnrollments = await context.Enrollments
                .Where(e => studentIds.Contains(e.UserId))
                .ToListAsync(ct);

            activeCurricula = await context.Curricula
                .Where(c => programIds.Contains(c.ProgramId) && c.IsActive)
                .ToListAsync(ct);
        }

        var results = new List<ChangeStudentLevelItemResult>();
        var auditLogs = new List<AuditLog>();
        var newEnrollments = new List<ProgramEnrollment>();

        foreach (var studentId in request.StudentIds)
        {
            var student = students.FirstOrDefault(s => s.Id == studentId);
            if (student == null)
            {
                results.Add(new ChangeStudentLevelItemResult(
                    studentId,
                    "Unknown Student",
                    null,
                    null,
                    null,
                    false,
                    "Student record not found."));
                continue;
            }

            var studentFullName = $"{student.FirstName} {student.LastName}".Trim();
            var oldLevelName = student.Level?.Name;

            // Resolve target AcademicLevel for this student
            AcademicLevel? resolvedLevel = null;

            if (student.AcademicProgramId.HasValue)
            {
                var levelsForProgram = programLevels
                    .Where(l => l.ProgramId == student.AcademicProgramId.Value)
                    .ToList();

                // 1. Direct ID match within program
                if (explicitTargetLevel != null && explicitTargetLevel.ProgramId == student.AcademicProgramId.Value)
                {
                    resolvedLevel = explicitTargetLevel;
                }
                // 2. Match by exact level name
                if (resolvedLevel == null && !string.IsNullOrWhiteSpace(targetName))
                {
                    resolvedLevel = levelsForProgram.FirstOrDefault(l =>
                        string.Equals(l.Name, targetName, StringComparison.OrdinalIgnoreCase));
                }
                // 3. Match by order
                if (resolvedLevel == null && targetOrder > 0)
                {
                    resolvedLevel = levelsForProgram.FirstOrDefault(l => l.Order == targetOrder);
                }
                // 4. Match by normalized digits (e.g. '100' or '200')
                if (resolvedLevel == null && !string.IsNullOrWhiteSpace(targetName))
                {
                    var normalizedTarget = new string(targetName.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(normalizedTarget))
                    {
                        resolvedLevel = levelsForProgram.FirstOrDefault(l =>
                            new string(l.Name.Where(char.IsDigit).ToArray()) == normalizedTarget);
                    }
                }
            }
            else
            {
                // Student has no program assigned yet: use explicitTargetLevel if available
                resolvedLevel = explicitTargetLevel;
            }

            if (resolvedLevel == null)
            {
                results.Add(new ChangeStudentLevelItemResult(
                    student.Id,
                    studentFullName,
                    student.StudentNumber,
                    oldLevelName,
                    null,
                    false,
                    $"Could not resolve matching level '{targetName}' for student's program ({student.AcademicProgram?.Name ?? "No Program assigned"})."));
                continue;
            }

            // Update student level
            student.LevelId = resolvedLevel.Id;
            student.UpdatedAt = DateTime.UtcNow;

            // Audit log
            auditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = changedByUserId ?? Guid.Empty,
                Action = "ChangeStudentLevel",
                EntityName = "Student",
                EntityId = student.Id.ToString(),
                Changes = $"Level changed from '{oldLevelName ?? "None"}' to '{resolvedLevel.Name}' (Program: {student.AcademicProgram?.Name ?? "None"})",
                Timestamp = DateTime.UtcNow
            });

            // Sync enrollment
            if (request.UpdateCurrentEnrollment)
            {
                var targetSessionId = activeSession?.Id ?? student.AcademicSessionId;
                if (targetSessionId != Guid.Empty)
                {
                    var currentEnrollment = existingEnrollments.FirstOrDefault(e =>
                        e.UserId == student.Id && e.AcademicSessionId == targetSessionId);

                    if (currentEnrollment != null)
                    {
                        currentEnrollment.LevelId = resolvedLevel.Id;
                        if (student.AcademicProgramId.HasValue && currentEnrollment.ProgramId != student.AcademicProgramId.Value)
                        {
                            currentEnrollment.ProgramId = student.AcademicProgramId.Value;
                        }
                    }
                    else if (student.AcademicProgramId.HasValue)
                    {
                        var curriculum = activeCurricula.FirstOrDefault(c => c.ProgramId == student.AcademicProgramId.Value)
                            ?? await context.Curricula.FirstOrDefaultAsync(c => c.ProgramId == student.AcademicProgramId.Value, ct);

                        if (curriculum != null)
                        {
                            var newEnrollment = new ProgramEnrollment
                            {
                                Id = Guid.NewGuid(),
                                ProgramId = student.AcademicProgramId.Value,
                                LevelId = resolvedLevel.Id,
                                UserId = student.Id,
                                AcademicSessionId = targetSessionId,
                                CurriculumId = curriculum.Id,
                                EnrolledAtUtc = DateTime.UtcNow
                            };
                            newEnrollments.Add(newEnrollment);
                            existingEnrollments.Add(newEnrollment);
                        }
                    }
                }
            }

            results.Add(new ChangeStudentLevelItemResult(
                student.Id,
                studentFullName,
                student.StudentNumber,
                oldLevelName,
                resolvedLevel.Name,
                true,
                $"Successfully updated level to {resolvedLevel.Name}."));
        }

        if (auditLogs.Count > 0)
        {
            context.AuditLogs.AddRange(auditLogs);
        }

        if (newEnrollments.Count > 0)
        {
            context.Enrollments.AddRange(newEnrollments);
        }

        await context.SaveChangesAsync(ct);

        var successfulCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);

        return new BatchChangeStudentLevelResponse(
            request.StudentIds.Count,
            successfulCount,
            failedCount,
            results);
    }

    public async Task<ChangeStudentProgramItemResult> ChangeStudentProgramAsync(
        Guid studentId, ChangeStudentProgramRequest request, Guid? changedByUserId, CancellationToken ct)
    {
        var batchReq = new BatchChangeStudentProgramRequest(
            [studentId],
            request.TargetProgramId,
            request.TargetLevelId,
            request.TargetLevelName,
            request.UpdateCurrentEnrollment,
            request.Reason,
            request.NewJambRegistrationNumber,
            request.JambDocumentUrl);

        var batchRes = await BatchChangeStudentProgramAsync(batchReq, changedByUserId, ct);
        return batchRes.Results.FirstOrDefault() 
            ?? new ChangeStudentProgramItemResult(studentId, "Unknown", null, null, null, false, false, "Student not found or program update failed.");
    }

    public async Task<BatchChangeStudentProgramResponse> BatchChangeStudentProgramAsync(
        BatchChangeStudentProgramRequest request, Guid? changedByUserId, CancellationToken ct)
    {
        if (request.StudentIds == null || !request.StudentIds.Any())
        {
            return new BatchChangeStudentProgramResponse(0, 0, 0, []);
        }

        var targetProgram = await context.Programs
            .Include(p => p.Department)
            .FirstOrDefaultAsync(p => p.Id == request.TargetProgramId, ct)
            ?? await context.Programs.FirstOrDefaultAsync(p => p.Id == request.TargetProgramId, ct);

        if (targetProgram == null)
        {
            var failedResults = request.StudentIds.Select(id => new ChangeStudentProgramItemResult(
                id, "Student", null, null, null, false, false, $"Target program with ID '{request.TargetProgramId}' was not found.")).ToList();
            return new BatchChangeStudentProgramResponse(request.StudentIds.Count, 0, request.StudentIds.Count, failedResults);
        }

        // Load levels for target program
        var targetProgramLevels = await context.Levels
            .Where(l => l.ProgramId == targetProgram.Id)
            .OrderBy(l => l.Order)
            .ToListAsync(ct);

        AcademicLevel? explicitTargetLevel = null;
        if (request.TargetLevelId.HasValue)
        {
            explicitTargetLevel = targetProgramLevels.FirstOrDefault(l => l.Id == request.TargetLevelId.Value)
                ?? await context.Levels.FirstOrDefaultAsync(l => l.Id == request.TargetLevelId.Value, ct);
        }

        var targetLevelName = (request.TargetLevelName ?? explicitTargetLevel?.Name ?? string.Empty).Trim();
        var targetLevelOrder = explicitTargetLevel?.Order ?? 0;

        var students = await context.Students
            .Include(s => s.Level)
            .Include(s => s.AcademicProgram)
            .Where(s => request.StudentIds.Contains(s.Id))
            .ToListAsync(ct);

        AcademicSession? activeSession = null;
        List<ProgramEnrollment> existingEnrollments = [];
        Curriculum? targetCurriculum = null;

        if (request.UpdateCurrentEnrollment)
        {
            activeSession = await context.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive, ct);
            var studentIds = students.Select(s => s.Id).ToList();
            existingEnrollments = await context.Enrollments
                .Where(e => studentIds.Contains(e.UserId))
                .ToListAsync(ct);

            targetCurriculum = await context.Curricula
                .FirstOrDefaultAsync(c => c.ProgramId == targetProgram.Id && c.IsActive, ct)
                ?? await context.Curricula.FirstOrDefaultAsync(c => c.ProgramId == targetProgram.Id, ct);
        }

        var results = new List<ChangeStudentProgramItemResult>();
        var auditLogs = new List<AuditLog>();
        var switchRequests = new List<ProgramSwitchRequest>();
        var newEnrollments = new List<ProgramEnrollment>();

        foreach (var studentId in request.StudentIds)
        {
            var student = students.FirstOrDefault(s => s.Id == studentId);
            if (student == null)
            {
                results.Add(new ChangeStudentProgramItemResult(
                    studentId,
                    "Unknown Student",
                    null,
                    null,
                    targetProgram.Name,
                    false,
                    false,
                    "Student record not found."));
                continue;
            }

            var studentFullName = $"{student.FirstName} {student.LastName}".Trim();
            var oldProgramName = student.AcademicProgram?.Name;
            var oldProgramId = student.AcademicProgramId;
            var isTransfer = oldProgramId.HasValue && oldProgramId.Value != targetProgram.Id;

            // Resolve target AcademicLevel for this student within target program
            AcademicLevel? resolvedLevel = explicitTargetLevel;

            if (resolvedLevel == null && !string.IsNullOrWhiteSpace(targetLevelName))
            {
                // Exact name
                resolvedLevel = targetProgramLevels.FirstOrDefault(l =>
                    string.Equals(l.Name, targetLevelName, StringComparison.OrdinalIgnoreCase));

                // Order
                if (resolvedLevel == null && targetLevelOrder > 0)
                {
                    resolvedLevel = targetProgramLevels.FirstOrDefault(l => l.Order == targetLevelOrder);
                }

                // Normalized digits (e.g. "100")
                if (resolvedLevel == null)
                {
                    var normalizedTarget = new string(targetLevelName.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(normalizedTarget))
                    {
                        resolvedLevel = targetProgramLevels.FirstOrDefault(l =>
                            new string(l.Name.Where(char.IsDigit).ToArray()) == normalizedTarget);
                    }
                }
            }

            // If not explicitly targeted, try to preserve student's current level equivalent in new program
            if (resolvedLevel == null && student.Level != null)
            {
                resolvedLevel = targetProgramLevels.FirstOrDefault(l =>
                    string.Equals(l.Name, student.Level.Name, StringComparison.OrdinalIgnoreCase));

                if (resolvedLevel == null)
                {
                    resolvedLevel = targetProgramLevels.FirstOrDefault(l => l.Order == student.Level.Order);
                }

                if (resolvedLevel == null)
                {
                    var currentDigits = new string(student.Level.Name.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(currentDigits))
                    {
                        resolvedLevel = targetProgramLevels.FirstOrDefault(l =>
                            new string(l.Name.Where(char.IsDigit).ToArray()) == currentDigits);
                    }
                }
            }

            // Fallback: lowest level in target program
            resolvedLevel ??= targetProgramLevels.FirstOrDefault();

            // Update student entity
            student.AcademicProgramId = targetProgram.Id;
            if (targetProgram.Department != null && targetProgram.Department.FacultyId != Guid.Empty)
            {
                student.FacultyId = targetProgram.Department.FacultyId;
            }
            if (resolvedLevel != null)
            {
                student.LevelId = resolvedLevel.Id;
            }
            if (!string.IsNullOrWhiteSpace(request.NewJambRegistrationNumber))
            {
                student.JambRegistrationNumber = request.NewJambRegistrationNumber;
            }
            student.UpdatedAt = DateTime.UtcNow;

            if (isTransfer)
            {
                // Create completed ProgramSwitchRequest for audit/tracking
                var switchRequest = new ProgramSwitchRequest
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    FromProgramId = oldProgramId!.Value,
                    ToProgramId = targetProgram.Id,
                    Reason = request.Reason ?? "Administrative program transfer by Registry/Admin",
                    Status = ProgramSwitchStatus.Completed,
                    RequiresJambAdmission = !string.IsNullOrWhiteSpace(request.NewJambRegistrationNumber) || !string.IsNullOrWhiteSpace(request.JambDocumentUrl),
                    NewJambRegistrationNumber = request.NewJambRegistrationNumber,
                    JambDocumentUrl = request.JambDocumentUrl,
                    HoDReviewedById = changedByUserId,
                    HoDReviewedAt = DateTime.UtcNow,
                    HoDNotes = "Auto-approved via Registry/Admin transfer",
                    DeanReviewedById = changedByUserId,
                    DeanReviewedAt = DateTime.UtcNow,
                    DeanNotes = "Auto-approved via Registry/Admin transfer",
                    AdminCompletedById = changedByUserId,
                    AdminCompletedAt = DateTime.UtcNow,
                    AdminNotes = request.Reason ?? "Directly transferred by Registry/Admin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                switchRequests.Add(switchRequest);

                auditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = changedByUserId ?? Guid.Empty,
                    Action = "TransferStudentProgram",
                    EntityName = "Student",
                    EntityId = student.Id.ToString(),
                    Changes = $"Program transferred from '{oldProgramName ?? "None"}' to '{targetProgram.Name}' (Level: {resolvedLevel?.Name ?? "Unchanged"}). Reason: {request.Reason ?? "Registry/Admin transfer"}",
                    Timestamp = DateTime.UtcNow
                });

                // Mark previous in-progress degree audits as incomplete
                var priorAudits = await context.DegreeAudits
                    .Where(a => a.StudentId == student.Id && a.Status == DegreeAuditStatus.InProgress)
                    .ToListAsync(ct);

                foreach (var audit in priorAudits)
                {
                    audit.Status = DegreeAuditStatus.Incomplete;
                    audit.Summary = "Marked incomplete due to program transfer.";
                    audit.CompletedAt = DateTime.UtcNow;
                }

                // Generate new degree audit if service available
                if (degreeAuditService != null)
                {
                    try
                    {
                        await degreeAuditService.CreateDegreeAuditAsync(student.Id,
                            new CreateDegreeAuditRequest(student.Id, targetProgram.Id, null), changedByUserId ?? Guid.Empty, ct);
                    }
                    catch
                    {
                        // Non-fatal if degree audit creation fails due to missing curriculum requirements
                    }
                }
            }
            else
            {
                // Simple initial assignment or same program update
                auditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = changedByUserId ?? Guid.Empty,
                    Action = oldProgramId.HasValue ? "UpdateStudentProgram" : "AssignStudentProgram",
                    EntityName = "Student",
                    EntityId = student.Id.ToString(),
                    Changes = $"Program set to '{targetProgram.Name}' (Level: {resolvedLevel?.Name ?? "Unchanged"}).",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Sync enrollment
            if (request.UpdateCurrentEnrollment)
            {
                var targetSessionId = activeSession?.Id ?? student.AcademicSessionId;
                if (targetSessionId != Guid.Empty)
                {
                    var currentEnrollment = existingEnrollments.FirstOrDefault(e =>
                        e.UserId == student.Id && e.AcademicSessionId == targetSessionId);

                    if (currentEnrollment != null)
                    {
                        currentEnrollment.ProgramId = targetProgram.Id;
                        if (resolvedLevel != null)
                        {
                            currentEnrollment.LevelId = resolvedLevel.Id;
                        }
                        if (targetCurriculum != null)
                        {
                            currentEnrollment.CurriculumId = targetCurriculum.Id;
                        }
                    }
                    else if (resolvedLevel != null)
                    {
                        var curriculum = targetCurriculum
                            ?? await context.Curricula.FirstOrDefaultAsync(c => c.ProgramId == targetProgram.Id, ct);

                        if (curriculum != null)
                        {
                            var newEnrollment = new ProgramEnrollment
                            {
                                Id = Guid.NewGuid(),
                                ProgramId = targetProgram.Id,
                                LevelId = resolvedLevel.Id,
                                UserId = student.Id,
                                AcademicSessionId = targetSessionId,
                                CurriculumId = curriculum.Id,
                                EnrolledAtUtc = DateTime.UtcNow
                            };
                            newEnrollments.Add(newEnrollment);
                            existingEnrollments.Add(newEnrollment);
                        }
                    }
                }
            }

            var msg = isTransfer
                ? $"Successfully transferred student to {targetProgram.Name}."
                : $"Successfully assigned program {targetProgram.Name}.";

            results.Add(new ChangeStudentProgramItemResult(
                student.Id,
                studentFullName,
                student.StudentNumber,
                oldProgramName,
                targetProgram.Name,
                isTransfer,
                true,
                msg));
        }

        if (switchRequests.Count > 0)
        {
            context.ProgramSwitchRequests.AddRange(switchRequests);
        }

        if (auditLogs.Count > 0)
        {
            context.AuditLogs.AddRange(auditLogs);
        }

        if (newEnrollments.Count > 0)
        {
            context.Enrollments.AddRange(newEnrollments);
        }

        await context.SaveChangesAsync(ct);

        var successfulCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);

        return new BatchChangeStudentProgramResponse(
            request.StudentIds.Count,
            successfulCount,
            failedCount,
            results);
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
