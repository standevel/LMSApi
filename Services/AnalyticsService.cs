using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class AnalyticsService : BaseService, IAnalyticsService
{
    private readonly LmsDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public AnalyticsService(LmsDbContext dbContext, IAuditService auditService, INotificationService notificationService) : base(auditService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<ErrorOr<EnrollmentAnalyticsDto>> GetEnrollmentAnalyticsAsync(Guid? academicSessionId = null, CancellationToken ct = default)
    {
        // Resolve target session IDs: use selected session, or fall back to all active sessions
        List<Guid> targetSessionIds;
        if (academicSessionId.HasValue)
        {
            targetSessionIds = [academicSessionId.Value];
        }
        else
        {
            targetSessionIds = await _dbContext.AcademicSessions
                .Where(s => s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        var totalEnrollments = academicSessionId.HasValue
            ? await _dbContext.Enrollments.CountAsync(e => e.AcademicSessionId == academicSessionId.Value, ct)
            : await _dbContext.Enrollments.CountAsync(ct);

        var newEnrollments = await _dbContext.Enrollments
            .Where(e => targetSessionIds.Contains(e.AcademicSessionId))
            .CountAsync(ct);

        var droppedEnrollments = await _dbContext.Enrollments
            .Where(e => e.EnrolledAtUtc < DateTime.UtcNow.AddMonths(-6))
            .CountAsync(ct);

        var activeEnrollments = totalEnrollments - droppedEnrollments;

        var enrollmentEntities = await _dbContext.Enrollments
            .Include(e => e.Program)
                .ThenInclude(p => p!.Department)
                    .ThenInclude(d => d!.Faculty)
            .Where(e => targetSessionIds.Contains(e.AcademicSessionId))
            .ToListAsync(ct);

        var enrollmentsByProgram = enrollmentEntities
            .GroupBy(e => e.ProgramId)
            .Select(g => new EnrollmentByProgramDto(
                g.Key,
                g.First().Program?.Name ?? "Unknown",
                g.Count()))
            .ToList();

        var enrollmentsByFaculty = enrollmentEntities
            .GroupBy(e => e.Program?.Department?.FacultyId ?? Guid.Empty)
            .Select(g => new EnrollmentByFacultyDto(
                g.Key,
                g.First().Program?.Department?.Faculty?.Name ?? "Unknown",
                g.Count()))
            .ToList();

        return new EnrollmentAnalyticsDto(
            totalEnrollments,
            newEnrollments,
            totalEnrollments - newEnrollments,
            droppedEnrollments,
            activeEnrollments,
            enrollmentsByProgram,
            enrollmentsByFaculty,
            DateTime.UtcNow.AddMonths(-12),
            DateTime.UtcNow);
    }

    public async Task<ErrorOr<GraduationRatesDto>> GetGraduationRatesAsync(CancellationToken ct = default)
    {
        var completedSessions = await _dbContext.AcademicSessions
            .Where(s => s.EndDate < DateTime.UtcNow)
            .OrderByDescending(s => s.EndDate)
            .Take(5)
            .ToListAsync(ct);

        var graduationRates = new List<GraduationRateDto>();

        foreach (var session in completedSessions)
        {
            var programs = await _dbContext.Programs
                .Include(p => p.Levels)
                .ToListAsync(ct);

            foreach (var program in programs)
            {
                // Calculate expected graduates (students who enrolled 4+ years ago for bachelor's)
                var expectedGraduates = await _dbContext.Enrollments
                    .Where(e => e.ProgramId == program.Id
                        && e.AcademicSession.StartDate <= session.StartDate.AddYears(-program.DurationYears)
                        && e.AcademicSession.StartDate >= session.StartDate.AddYears(-program.DurationYears - 1))
                    .CountAsync(ct);

                // Calculate actual graduates (simplified - in production, track completion status)
                var totalGraduates = (int)(expectedGraduates * 0.85m); // Assume 85% graduation rate

                var graduationRate = expectedGraduates > 0 ? (decimal)totalGraduates / expectedGraduates * 100 : 0;

                graduationRates.Add(new GraduationRateDto(
                    program.Name,
                    totalGraduates,
                    expectedGraduates,
                    Math.Round(graduationRate, 2),
                    session.Name));
            }
        }

        return new GraduationRatesDto(
            graduationRates,
            completedSessions.LastOrDefault()?.StartDate ?? DateTime.UtcNow,
            completedSessions.FirstOrDefault()?.StartDate ?? DateTime.UtcNow);
    }

    public async Task<ErrorOr<DashboardSummaryDto>> GetDashboardSummaryAsync(Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var totalStudents = academicSessionId.HasValue
            ? await _dbContext.Enrollments.CountAsync(e => e.AcademicSessionId == academicSessionId.Value, ct)
            : await _dbContext.Students.CountAsync(ct);
        var totalLecturers = await _dbContext.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Lecturer"))
            .CountAsync(ct);
        var totalCourseOfferings = academicSessionId.HasValue
            ? await _dbContext.CourseOfferings.CountAsync(co => co.AcademicSessionId == academicSessionId.Value, ct)
            : await _dbContext.CourseOfferings.CountAsync(ct);
        var activeSessions = await _dbContext.AcademicSessions.CountAsync(s => s.IsActive);
        var totalPrograms = await _dbContext.Programs.CountAsync(p => p.IsActive);

        // Student GPA overview
        var allGrades = await _dbContext.Grades.Where(g => g.IsLocked).ToListAsync(ct);
        var averageGpa = allGrades.Any() ? Math.Round(allGrades.Average(g => g.MarksObtained) / 100 * 4, 2) : 0;
        var studentsOnProbation = await _dbContext.AcademicStandings.CountAsync(s => s.StandingType == AcademicStandingType.Probation);
        var studentsOnDeanList = await _dbContext.AcademicStandings.CountAsync(s => s.StandingType == AcademicStandingType.DeanList);
        var totalStudentsWithGpa = await _dbContext.Grades.Select(g => g.StudentId).Distinct().CountAsync(ct);

        // Enrollment trend
        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;
        var currentTotal = await _dbContext.Enrollments
            .Where(e => e.EnrolledAtUtc.Month == currentMonth && e.EnrolledAtUtc.Year == currentYear)
            .CountAsync(ct);

        var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
        var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;
        var previousTotal = await _dbContext.Enrollments
            .Where(e => e.EnrolledAtUtc.Month == previousMonth && e.EnrolledAtUtc.Year == previousYear)
            .CountAsync(ct);

        var growthPercentage = previousTotal > 0 ? (currentTotal - previousTotal) / (decimal)previousTotal * 100 : 0;

        var monthlyEnrollments = new List<MonthlyEnrollmentDto>();
        for (int i = 11; i >= 0; i--)
        {
            var month = currentMonth - i;
            var year = currentYear;
            if (month <= 0)
            {
                month += 12;
                year--;
            }

            var count = await _dbContext.Enrollments
                .Where(e => e.EnrolledAtUtc.Month == month && e.EnrolledAtUtc.Year == year)
                .CountAsync(ct);

            monthlyEnrollments.Add(new MonthlyEnrollmentDto(
                month,
                DateTime.MinValue.AddMonths(month).ToString("MMMM"),
                count));
        }

        // Department stats
        var departmentStats = await _dbContext.Departments
            .Include(d => d.Faculty)
            .Select(d => new
            {
                Department = d,
                ProgramCount = d.Programs.Count(),
                StudentCount = _dbContext.Enrollments.Where(e => e.Program.DepartmentId == d.Id).Count(),
                LecturerCount = _dbContext.Users.Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Lecturer")).Count()
            })
            .ToListAsync(ct);

        var deptStatsList = departmentStats.Select(d => new DepartmentStatsDto(
            d.Department.Id,
            d.Department.Name,
            d.Department.Faculty?.Name ?? "N/A",
            d.ProgramCount,
            0, // Would need proper join
            0)).ToList();

        return new DashboardSummaryDto(
            totalStudents,
            totalLecturers,
            totalCourseOfferings,
            activeSessions,
            totalPrograms,
            studentsOnProbation,
            new StudentGpaOverview(averageGpa, studentsOnProbation, studentsOnDeanList, totalStudentsWithGpa),
            new EnrollmentTrendDto(monthlyEnrollments, currentTotal, previousTotal, Math.Round(growthPercentage, 2)),
            deptStatsList);
    }

    public async Task<ErrorOr<FacultyDashboardDto>> GetFacultyDashboardAsync(Guid facultyId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var faculty = await _dbContext.Faculties
            .Include(f => f.Departments)
                .ThenInclude(d => d.Programs)
            .FirstOrDefaultAsync(f => f.Id == facultyId, ct);

        if (faculty == null)
            return Error.NotFound("Faculty.NotFound", "Faculty not found");

        var totalPrograms = faculty.Departments.Sum(d => d.Programs.Count());
        
        var enrollmentsQuery = _dbContext.Enrollments
            .Where(e => faculty.Departments.Select(d => d.Id).Contains(e.Program.DepartmentId));
            
        if (academicSessionId.HasValue)
            enrollmentsQuery = enrollmentsQuery.Where(e => e.AcademicSessionId == academicSessionId.Value);
            
        var totalStudents = await enrollmentsQuery.CountAsync(ct);
        var totalLecturers = await _dbContext.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Lecturer"))
            .CountAsync(ct);

        var departments = faculty.Departments.Select(d => new DepartmentSummaryDto(
            d.Id,
            d.Name,
            d.Code,
            d.Programs.Count(),
            0,
            0)).ToList();

        return new FacultyDashboardDto(
            facultyId,
            faculty.Name,
            faculty.Label,
            faculty.Departments.Count,
            totalPrograms,
            totalStudents,
            totalLecturers,
            departments);
    }

    public async Task<ErrorOr<DepartmentDashboardDto>> GetDepartmentDashboardAsync(Guid departmentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var department = await _dbContext.Departments
            .Include(d => d.Faculty)
            .Include(d => d.Programs)
            .FirstOrDefaultAsync(d => d.Id == departmentId, ct);

        if (department == null)
            return Error.NotFound("Department.NotFound", "Department not found");

        var totalStudents = 0;
        var programs = new List<ProgramStatsDto>();

        foreach (var program in department.Programs)
        {
            var studentCountQuery = _dbContext.Enrollments.Where(e => e.ProgramId == program.Id);
            if (academicSessionId.HasValue)
                studentCountQuery = studentCountQuery.Where(e => e.AcademicSessionId == academicSessionId.Value);
                
            var studentCount = await studentCountQuery.CountAsync(ct);

            var offeringsQuery = _dbContext.CourseOfferings.Where(co =>
                co.AcademicSessionId != null &&
                _dbContext.CourseOfferingPrograms.Any(p =>
                    p.CourseOfferingId == co.Id && p.Program.DepartmentId == department.Id));
            if (academicSessionId.HasValue)
                offeringsQuery = offeringsQuery.Where(co => co.AcademicSessionId == academicSessionId.Value);
            else
                offeringsQuery = offeringsQuery.Where(co => co.AcademicSession.IsActive);
                
            var activeOfferings = await offeringsQuery.CountAsync(ct);

            programs.Add(new ProgramStatsDto(
                program.Id,
                program.Name,
                program.Code,
                studentCount,
                activeOfferings,
                0)); // Would need GPA calculation

            totalStudents += studentCount;
        }

        var totalLecturers = await _dbContext.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Lecturer"))
            .CountAsync(ct);

        return new DepartmentDashboardDto(
            departmentId,
            department.Name,
            department.Faculty?.Name ?? "N/A",
            department.Programs.Count,
            totalStudents,
            totalLecturers,
            programs,
            new List<LecturerStatsDto>());
    }

    public async Task<ErrorOr<TreasuryAnalyticsDto>> GetTreasuryAnalyticsAsync(Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var sessionName = "All Sessions";
        if (academicSessionId.HasValue)
        {
            var session = await _dbContext.AcademicSessions.FindAsync(new object[] { academicSessionId.Value }, ct);
            if (session != null)
                sessionName = session.Name;
        }

        // Load fee records as lightweight projections — avoids EF GroupBy translation issues
        var recordsQuery = _dbContext.StudentFeeRecords
            .AsNoTracking()
            .AsQueryable();

        if (academicSessionId.HasValue)
            recordsQuery = recordsQuery.Where(r => r.SessionId == academicSessionId.Value);

        var records = await recordsQuery
            .Select(r => new
            {
                r.TotalAmount,
                r.AmountPaid,
                r.ScholarshipDiscount,
                r.GeneratedAt,
                StudentFacultyId   = r.Student.FacultyId,
                StudentFacultyName = r.Student.Faculty != null ? r.Student.Faculty.Name : null
            })
            .ToListAsync(ct);

        // Compute totals in memory — safe and readable
        decimal forecastedRevenue  = records.Sum(r => Math.Max(0, r.TotalAmount - r.ScholarshipDiscount));
        decimal totalCollected     = records.Sum(r => r.AmountPaid);
        decimal outstandingBalance = records.Sum(r => Math.Max(0, r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount));
        decimal totalScholarships  = records.Sum(r => r.ScholarshipDiscount);
        int studentsInArrears      = records.Count(r => r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount > 0);

        // Faculty revenue breakdown — group in memory
        var groupedByFaculty = records
            .Where(r => r.StudentFacultyId.HasValue && r.StudentFacultyName != null)
            .GroupBy(r => new { Id = r.StudentFacultyId!.Value, Name = r.StudentFacultyName! })
            .Select(g => new FacultyRevenueDto(
                g.Key.Id,
                g.Key.Name,
                g.Sum(r => Math.Max(0, r.TotalAmount - r.ScholarshipDiscount)),
                g.Sum(r => r.AmountPaid)
            ))
            .OrderByDescending(f => f.ExpectedRevenue)
            .ToList();

        // Monthly revenue trend — confirmed payments over the last 12 months
        var trendStart = new DateTime(DateTime.UtcNow.AddMonths(-11).Year, DateTime.UtcNow.AddMonths(-11).Month, 1);

        var paymentsQuery = _dbContext.FeePayments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Confirmed && p.PaidAt >= trendStart);

        if (academicSessionId.HasValue)
            paymentsQuery = paymentsQuery.Where(p => p.StudentFeeRecord.SessionId == academicSessionId.Value);

        var rawPayments = await paymentsQuery
            .Select(p => new { p.PaidAt.Year, p.PaidAt.Month, p.Amount })
            .ToListAsync(ct);

        var trendList = new List<MonthlyRevenueDto>();
        for (int i = 11; i >= 0; i--)
        {
            var d = DateTime.UtcNow.AddMonths(-i);
            var collected = rawPayments
                .Where(p => p.Year == d.Year && p.Month == d.Month)
                .Sum(p => p.Amount);
            var expected = records
                .Where(r => r.GeneratedAt.Year == d.Year && r.GeneratedAt.Month == d.Month)
                .Sum(r => Math.Max(0, r.TotalAmount - r.ScholarshipDiscount));
            trendList.Add(new MonthlyRevenueDto(d.Month, d.ToString("MMM"), d.Year, collected, expected));
        }

        return new TreasuryAnalyticsDto(
            totalCollected,
            forecastedRevenue,
            totalScholarships,
            outstandingBalance,
            studentsInArrears,
            groupedByFaculty,
            trendList,
            sessionName,
            DateTime.UtcNow
        );
    }

    public async Task<ErrorOr<FeeLedgerResponseDto>> GetFeeLedgerAsync(FeeLedgerRequestDto request, CancellationToken ct = default)
    {
        var query = _dbContext.FeePayments
            .Include(p => p.StudentFeeRecord)
                .ThenInclude(r => r.Student)
                    .ThenInclude(s => s.Faculty)
            .Include(p => p.StudentFeeRecord)
                .ThenInclude(r => r.Session)
            .AsQueryable();

        if (request.SessionId.HasValue)
            query = query.Where(p => p.StudentFeeRecord.SessionId == request.SessionId.Value);

        if (request.StartDate.HasValue)
            query = query.Where(p => p.PaidAt >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(p => p.PaidAt <= request.EndDate.Value.AddDays(1).AddTicks(-1));

        if (request.PaymentMethod.HasValue)
            query = query.Where(p => p.PaymentMethod == (PaymentMethod)request.PaymentMethod.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.ToLower();
            query = query.Where(p =>
                p.StudentFeeRecord.Student.FirstName.ToLower().Contains(search) ||
                p.StudentFeeRecord.Student.LastName.ToLower().Contains(search) ||
                (p.StudentFeeRecord.Student.StudentNumber != null && p.StudentFeeRecord.Student.StudentNumber.ToLower().Contains(search)) ||
                (p.ReferenceNumber != null && p.ReferenceNumber.ToLower().Contains(search)) ||
                (p.GatewayReference != null && p.GatewayReference.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(ct);

        // When exporting all, skip paging; otherwise apply normal pagination
        var pagedQuery = request.ExportAll
            ? query.OrderByDescending(p => p.PaidAt)
            : query.OrderByDescending(p => p.PaidAt)
                   .Skip((request.Page - 1) * request.PageSize)
                   .Take(request.PageSize);

        // Load with template/category info via a separate join for each payment
        var rawPayments = await pagedQuery
            .Select(p => new
            {
                p.Id,
                p.StudentFeeRecord.StudentId,
                StudentName  = p.StudentFeeRecord.Student.FirstName + " " + p.StudentFeeRecord.Student.LastName,
                MatricNumber = p.StudentFeeRecord.Student.StudentNumber ?? "",
                SessionName  = p.StudentFeeRecord.Session.Name,
                p.Amount,
                PaymentMethod = (int)p.PaymentMethod,
                Reference     = p.ReferenceNumber ?? p.GatewayReference ?? "",
                p.PaidAt,
                Status        = p.Status.ToString(),
                FacultyName   = p.StudentFeeRecord.Student.Faculty != null ? p.StudentFeeRecord.Student.Faculty.Name : "",
                // Resolve category/template via the fee assignments on the StudentFeeRecord
                FeeRecordId   = p.StudentFeeRecordId
            })
            .ToListAsync(ct);

        // Resolve template/category names: find the most-specific active assignment that covers each student fee record
        var feeRecordIds = rawPayments.Select(p => p.FeeRecordId).Distinct().ToList();

        var templatesByRecord = await _dbContext.StudentFeeRecords
            .Where(r => feeRecordIds.Contains(r.Id))
            .Select(r => new
            {
                FeeRecordId = r.Id,
                StudentId   = r.StudentId,
                SessionId   = r.SessionId
            })
            .ToListAsync(ct);

        // Map studentId+sessionId to their templates via assignments
        var studentSessionPairs = templatesByRecord
            .Select(x => new { x.StudentId, x.SessionId })
            .Distinct()
            .ToList();

        // Pull relevant assignments with category info
        var assignmentLookup = await _dbContext.FeeAssignments
            .Include(a => a.FeeTemplate).ThenInclude(t => t.Category)
            .Where(a => a.IsActive &&
                        a.SessionId.HasValue &&
                        studentSessionPairs.Select(p => p.SessionId).Contains(a.SessionId!.Value))
            .Select(a => new
            {
                a.SessionId,
                a.StudentId,
                a.FacultyId,
                a.ProgramId,
                TemplateName = a.FeeTemplate.Name,
                CategoryName = a.FeeTemplate.Category != null ? a.FeeTemplate.Category.Name : "General",
                a.FeeTemplate.Scope
            })
            .ToListAsync(ct);

        // Build student → (templateName, categoryName) lookup; prefer most specific scope
        var studentScopeLookup = new Dictionary<(Guid studentId, Guid sessionId), (string template, string category)>();
        foreach (var sr in templatesByRecord)
        {
            var candidates = assignmentLookup
                .Where(a => a.SessionId == sr.SessionId)
                .OrderByDescending(a => a.StudentId == sr.StudentId ? 3
                    : a.Scope.ToString() == "Student" ? 3
                    : a.Scope.ToString() == "Program"  ? 2
                    : a.Scope.ToString() == "Faculty"   ? 1 : 0)
                .FirstOrDefault();

            studentScopeLookup[(sr.StudentId, sr.SessionId)] = candidates != null
                ? (candidates.TemplateName, candidates.CategoryName)
                : ("–", "General");
        }

        var payments = rawPayments.Select(p =>
        {
            var sr = templatesByRecord.FirstOrDefault(r => r.FeeRecordId == p.FeeRecordId);
            var (template, category) = sr != null && studentScopeLookup.TryGetValue((sr.StudentId, sr.SessionId), out var names)
                ? names
                : ("–", "General");

            return new FeePaymentRecordDto(
                p.Id,
                p.StudentId,
                p.StudentName,
                p.MatricNumber,
                p.SessionName,
                p.Amount,
                p.PaymentMethod,
                p.Reference,
                p.PaidAt,
                p.Status,
                category,
                template,
                p.FacultyName);
        }).ToList();

        return new FeeLedgerResponseDto(payments, totalCount);
    }

    public async Task<ErrorOr<DebtorsReportResponseDto>> GetDebtorsReportAsync(DebtorsReportRequestDto request, CancellationToken ct = default)
    {
        // Strategy: start from StudentFeeRecords (students who have had bills generated).
        // For students with no bill, we can't infer a specific amount, so only show billed debtors.
        // The UI should prompt bursary staff to generate bills first if the list looks short.
        var query = _dbContext.StudentFeeRecords
            .Include(r => r.Student)
                .ThenInclude(s => s.AcademicProgram)
            .Include(r => r.Student)
                .ThenInclude(s => s.Level)
            .Include(r => r.Student)
                .ThenInclude(s => s.Faculty)
            .Include(r => r.Payments)
            .AsNoTracking()
            .Where(r => r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount > 0)
            .AsQueryable();

        if (request.SessionId.HasValue)
            query = query.Where(r => r.SessionId == request.SessionId.Value);

        if (request.FacultyId.HasValue)
            query = query.Where(r => r.Student.FacultyId == request.FacultyId.Value);

        if (request.DepartmentId.HasValue)
            query = query.Where(r => r.Student.AcademicProgram != null && r.Student.AcademicProgram.DepartmentId == request.DepartmentId.Value);

        if (request.ProgramId.HasValue)
            query = query.Where(r => r.Student.AcademicProgramId == request.ProgramId.Value);

        if (request.LevelId.HasValue)
            query = query.Where(r => r.Student.LevelId == request.LevelId.Value);

        var totalCount = await query.CountAsync(ct);
        var totalDebt  = await query.SumAsync(r => r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount, ct);

        var rawDebtors = request.ExportAll
            ? await query.OrderByDescending(r => r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount).ToListAsync(ct)
            : await query.OrderByDescending(r => r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount)
                         .Skip((request.Page - 1) * request.PageSize)
                         .Take(request.PageSize)
                         .ToListAsync(ct);

        // Also include students enrolled but with NO fee record yet (they owe but have no bill)
        var billedStudentIds = rawDebtors.Select(r => r.StudentId).ToHashSet();

        // Find enrolled students without any fee record for this session
        var unbilledStudentsQuery = _dbContext.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Level)
            .Include(s => s.Faculty)
            .AsNoTracking()
            .Where(s => !_dbContext.StudentFeeRecords.Any(r =>
                r.StudentId == s.Id &&
                (!request.SessionId.HasValue || r.SessionId == request.SessionId.Value)));

        if (request.FacultyId.HasValue)
            unbilledStudentsQuery = unbilledStudentsQuery.Where(s => s.FacultyId == request.FacultyId.Value);

        if (request.ProgramId.HasValue)
            unbilledStudentsQuery = unbilledStudentsQuery.Where(s => s.AcademicProgramId == request.ProgramId.Value);

        if (request.LevelId.HasValue)
            unbilledStudentsQuery = unbilledStudentsQuery.Where(s => s.LevelId == request.LevelId.Value);

        // Only include unbilled students if they are enrolled in the session
        List<Student> unbilledStudents;
        if (request.SessionId.HasValue)
        {
            var enrolledUserIds = await _dbContext.Enrollments
                .Where(e => e.AcademicSessionId == request.SessionId.Value)
                .Select(e => e.UserId)
                .ToListAsync(ct);

            // Map AppUser IDs to Student IDs via OfficialEmail or EntraObjectId match
            // Simplest: cross-reference via the Student table against enrolled user GUIDs
            // ProgramEnrollment.UserId == Student.Id when the student is an AppUser-backed record
            unbilledStudents = await unbilledStudentsQuery
                .Where(s => enrolledUserIds.Contains(s.Id))
                .ToListAsync(ct);
        }
        else
        {
            unbilledStudents = new List<Student>(); // skip unbilled if no session filter
        }

        // Default bill amount for unbilled students = sum of all active templates for session
        decimal defaultBillAmount = unbilledStudents.Any()
            ? await _dbContext.FeeTemplates
                .Where(t => t.IsActive && (t.SessionId == null || t.SessionId == request.SessionId))
                .SelectMany(t => t.LineItems)
                .SumAsync(li => li.Amount, ct)
            : 0;

        var now = DateTime.UtcNow;
        var dueDates = await _dbContext.FeeAssignments
            .Where(a => a.IsActive
                && (request.SessionId == null || a.SessionId == request.SessionId)
                && (a.DueDateOverride.HasValue || a.FeeTemplate.DueDate.HasValue))
            .Select(a => new { a.StudentId, DueDate = a.DueDateOverride ?? a.FeeTemplate.DueDate })
            .ToListAsync(ct);

        var debtors = rawDebtors.Select(r =>
        {
            var outstanding  = Math.Max(0, r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount);
            var lastPayment  = r.Payments
                .Where(p => p.Status == PaymentStatus.Confirmed)
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefault()?.PaidAt;
            var due = dueDates.Where(d => d.StudentId == r.StudentId).Select(d => d.DueDate).Min();
            var daysOverdue  = due.HasValue && due.Value < now ? (int)(now - due.Value).TotalDays : 0;

            return new DebtorRecordDto(
                r.StudentId,
                r.Student.FirstName + " " + r.Student.LastName,
                r.Student.StudentNumber ?? "",
                r.Student.AcademicProgram?.Name ?? "",
                r.Student.Level?.Name ?? "",
                r.Student.Faculty?.Name ?? "",
                r.TotalAmount, r.AmountPaid, outstanding, daysOverdue, lastPayment);
        }).ToList();

        // Append unbilled students
        foreach (var s in unbilledStudents.Where(s => !billedStudentIds.Contains(s.Id)))
        {
            var due = dueDates.Where(d => d.StudentId == s.Id).Select(d => d.DueDate).Min();
            var daysOverdue = due.HasValue && due.Value < now ? (int)(now - due.Value).TotalDays : 0;

            debtors.Add(new DebtorRecordDto(
                s.Id,
                s.FirstName + " " + s.LastName,
                s.StudentNumber ?? "",
                s.AcademicProgram?.Name ?? "",
                s.Level?.Name ?? "",
                s.Faculty?.Name ?? "",
                defaultBillAmount, 0, defaultBillAmount, daysOverdue, null));
        }

        var allCount = totalCount + unbilledStudents.Count(s => !billedStudentIds.Contains(s.Id));
        var allDebt  = totalDebt  + debtors.Where(d => !billedStudentIds.Contains(d.StudentId)).Sum(d => d.OutstandingBalance);

        return new DebtorsReportResponseDto(debtors, allCount, allDebt);
    }

    public async Task<ErrorOr<List<RevenueByCategoryDto>>> GetRevenueByCategoryAsync(Guid? academicSessionId = null, CancellationToken ct = default)
    {
        // Step 1: Load all active fee templates with their categories and line items
        var templateQuery = _dbContext.FeeTemplates
            .Include(t => t.Category)
            .Include(t => t.LineItems)
            .AsNoTracking()
            .Where(t => t.IsActive);

        if (academicSessionId.HasValue)
            templateQuery = templateQuery.Where(t => t.SessionId == null || t.SessionId == academicSessionId);

        var templates = await templateQuery.ToListAsync(ct);

        if (!templates.Any())
            return new List<RevenueByCategoryDto>();

        // Step 2: Count enrolled students per template scope so we can compute expected revenue
        // (even if bills haven't been generated yet)
        Guid? sessionFilter = academicSessionId;

        var enrolledStudentCount = sessionFilter.HasValue
            ? await _dbContext.Enrollments.CountAsync(e => e.AcademicSessionId == sessionFilter.Value, ct)
            : await _dbContext.Students.CountAsync(ct);

        if (enrolledStudentCount == 0) enrolledStudentCount = 1; // avoid zero multiplier

        // Step 3: Group templates by category and compute expected revenue per category
        // Expected = template total × number of students the template applies to
        var categoryGroups = templates
            .GroupBy(t => t.Category?.Name ?? "General")
            .ToDictionary(g => g.Key, g => g.Sum(t => t.LineItems.Sum(li => li.Amount)));

        // Step 4: Get confirmed payments, scoped to session
        var paymentsQuery = _dbContext.FeePayments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Confirmed);

        if (academicSessionId.HasValue)
            paymentsQuery = paymentsQuery.Where(p => p.StudentFeeRecord.SessionId == academicSessionId.Value);

        var totalCollected = await paymentsQuery.SumAsync(p => p.Amount, ct);

        // Step 5: Get StudentFeeRecords to compute expected and collected per student
        var feeRecordsQuery = _dbContext.StudentFeeRecords.AsNoTracking();
        if (academicSessionId.HasValue)
            feeRecordsQuery = feeRecordsQuery.Where(r => r.SessionId == academicSessionId.Value);

        var feeRecords = await feeRecordsQuery
            .Select(r => new { r.Id, r.TotalAmount, r.ScholarshipDiscount })
            .ToListAsync(ct);

        var confirmedPayments = await paymentsQuery
            .Select(p => new { p.StudentFeeRecordId, p.Amount })
            .ToListAsync(ct);

        var totalExpected = feeRecords.Sum(r => Math.Max(0, r.TotalAmount - r.ScholarshipDiscount));
        var totalTemplateAmount = categoryGroups.Values.Sum();
        if (totalTemplateAmount == 0) totalTemplateAmount = 1;

        var collectedByRecord = confirmedPayments
            .GroupBy(p => p.StudentFeeRecordId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var result = new List<RevenueByCategoryDto>();

        foreach (var (categoryName, categoryTemplateTotal) in categoryGroups)
        {
            var proportion = categoryTemplateTotal / totalTemplateAmount;

            decimal expectedForCategory;
            decimal collectedForCategory;

            if (feeRecords.Any())
            {
                // Apportion actual bills and payments
                expectedForCategory  = feeRecords.Sum(r => Math.Max(0, r.TotalAmount - r.ScholarshipDiscount) * proportion);
                collectedForCategory = feeRecords.Sum(r =>
                {
                    var netBill = Math.Max(0, r.TotalAmount - r.ScholarshipDiscount);
                    var paid = collectedByRecord.GetValueOrDefault(r.Id, 0);
                    return Math.Min(paid, netBill) * proportion;
                });
            }
            else
            {
                // No bills generated yet — estimate from template amounts × enrolled count
                expectedForCategory  = categoryTemplateTotal * enrolledStudentCount;
                collectedForCategory = totalCollected * proportion;
            }

            result.Add(new RevenueByCategoryDto(
                categoryName,
                Math.Round(expectedForCategory, 2),
                Math.Round(collectedForCategory, 2),
                Math.Round(Math.Max(0, expectedForCategory - collectedForCategory), 2)));
        }

        return result.OrderByDescending(r => r.ExpectedRevenue).ToList();
    }

    public async Task<ErrorOr<List<ScholarshipImpactDto>>> GetScholarshipImpactAsync(Guid? academicSessionId = null, CancellationToken ct = default)
    {
        // Load all active scholarships with their sponsor orgs
        var scholarships = await _dbContext.Scholarships
            .Include(s => s.SponsorOrganization)
            .AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        if (!scholarships.Any())
            return new List<ScholarshipImpactDto>();

        // Load all student scholarship assignments, filtered by session if provided
        var assignmentsQuery = _dbContext.StudentScholarships
            .AsNoTracking()
            .Where(ss => scholarships.Select(s => s.Id).Contains(ss.ScholarshipId));

        if (academicSessionId.HasValue)
            assignmentsQuery = assignmentsQuery.Where(ss => ss.SessionId == academicSessionId.Value);

        var assignments = await assignmentsQuery
            .Select(ss => new { ss.ScholarshipId, ss.CalculatedAmount })
            .ToListAsync(ct);

        // Group by scholarship — include scholarships with zero beneficiaries (defined but not yet applied)
        var result = scholarships.Select(s =>
        {
            var studentAssignments = assignments.Where(a => a.ScholarshipId == s.Id).ToList();
            return new ScholarshipImpactDto(
                s.Name,
                s.Type.ToString(),
                s.SponsorOrganization?.Name ?? "N/A",
                studentAssignments.Count,
                studentAssignments.Sum(a => a.CalculatedAmount));
        })
        .OrderByDescending(r => r.TotalDiscountApplied)
        .ThenByDescending(r => r.StudentsBenefited)
        .ToList();

        return result;
    }

    public async Task<ErrorOr<FeeReminderResult>> SendFeeRemindersAsync(Guid? academicSessionId, CancellationToken ct = default)
    {
        // Find all students with an outstanding balance
        var query = _dbContext.StudentFeeRecords
            .Include(r => r.Student)
            .Include(r => r.Session)
            .Where(r => (r.TotalAmount - r.AmountPaid - r.ScholarshipDiscount) > 0);

        if (academicSessionId.HasValue)
            query = query.Where(r => r.SessionId == academicSessionId.Value);

        var debtorRecords = await query.ToListAsync(ct);

        if (!debtorRecords.Any())
            return new FeeReminderResult(0, 0, "No outstanding balances found — no reminders sent.");

        var queued = 0;
        foreach (var record in debtorRecords)
        {
            var outstanding = Math.Max(0, record.TotalAmount - record.AmountPaid - record.ScholarshipDiscount);
            var sessionName = record.Session?.Name ?? "the current session";

            var notificationRequest = new CreateNotificationRequest(
                record.StudentId,
                null,
                "Fee Payment Reminder",
                $"You have an outstanding balance of ₦{outstanding:N0} for {sessionName}. " +
                $"Please log in to the student portal to settle your fees before the due date to avoid late payment charges.",
                "Finance",
                "/dashboard/student/fees"
            );

            var notifResult = await _notificationService.CreateAsync(notificationRequest, ct);
            if (!notifResult.IsError)
                queued++;
        }

        return new FeeReminderResult(
            debtorRecords.Count,
            queued,
            $"Reminders sent to {queued} of {debtorRecords.Count} students with outstanding balances.");
    }
}
