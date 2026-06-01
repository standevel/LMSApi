using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class AnalyticsService : BaseService, IAnalyticsService
{
    private readonly LmsDbContext _dbContext;

    public AnalyticsService(LmsDbContext dbContext, IAuditService auditService) : base(auditService)
    {
        _dbContext = dbContext;
    }

    public async Task<ErrorOr<EnrollmentAnalyticsDto>> GetEnrollmentAnalyticsAsync(CancellationToken ct = default)
    {
        var totalEnrollments = await _dbContext.Enrollments.CountAsync(ct);
        var activeSessions = await _dbContext.AcademicSessions.Where(s => s.IsActive).Select(s => s.Id).ToListAsync(ct);

        var newEnrollments = await _dbContext.Enrollments
            .Where(e => activeSessions.Contains(e.AcademicSessionId))
            .CountAsync(ct);

        var droppedEnrollments = await _dbContext.Enrollments
            .Where(e => e.EnrolledAtUtc < DateTime.UtcNow.AddMonths(-6))
            .CountAsync(ct);

        var activeEnrollments = totalEnrollments - droppedEnrollments;

        var enrollmentEntities = await _dbContext.Enrollments
            .Include(e => e.Program)
                .ThenInclude(p => p!.Department)
                    .ThenInclude(d => d!.Faculty)
            .Where(e => activeSessions.Contains(e.AcademicSessionId))
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

    public async Task<ErrorOr<DashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var totalStudents = await _dbContext.Students.CountAsync(ct);
        var totalLecturers = await _dbContext.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Lecturer"))
            .CountAsync(ct);
        var totalCourseOfferings = await _dbContext.CourseOfferings.CountAsync(ct);
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

    public async Task<ErrorOr<FacultyDashboardDto>> GetFacultyDashboardAsync(Guid facultyId, CancellationToken ct = default)
    {
        var faculty = await _dbContext.Faculties
            .Include(f => f.Departments)
                .ThenInclude(d => d.Programs)
            .FirstOrDefaultAsync(f => f.Id == facultyId, ct);

        if (faculty == null)
            return Error.NotFound("Faculty.NotFound", "Faculty not found");

        var totalPrograms = faculty.Departments.Sum(d => d.Programs.Count());
        var totalStudents = await _dbContext.Enrollments
            .Where(e => faculty.Departments.Select(d => d.Id).Contains(e.Program.DepartmentId))
            .CountAsync(ct);
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

    public async Task<ErrorOr<DepartmentDashboardDto>> GetDepartmentDashboardAsync(Guid departmentId, CancellationToken ct = default)
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
            var studentCount = await _dbContext.Enrollments
                .CountAsync(e => e.ProgramId == program.Id, ct);

            var activeOfferings = await _dbContext.CourseOfferings
                .CountAsync(co => co.ProgramId == program.Id && co.AcademicSession.IsActive, ct);

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
}
