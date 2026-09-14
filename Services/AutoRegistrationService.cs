using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public class AutoRegistrationService : BaseService, IAutoRegistrationService
{
    private readonly LmsDbContext _context;
    private readonly ILogger<AutoRegistrationService> _logger;

    public AutoRegistrationService(
        LmsDbContext context,
        IAuditService auditService,
        ILogger<AutoRegistrationService> logger) : base(auditService)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<StudentAutoRegistrationDetailDto> AutoRegisterStudentAsync(
        Guid studentId,
        Guid academicSessionId,
        bool isDryRun = false,
        Semester? targetSemester = null,
        CancellationToken ct = default)
    {
        var studentUser = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == studentId, ct);
        var studentProfile = await _context.Set<Student>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId || (studentUser != null && (s.OfficialEmail == studentUser.Email || s.PersonalEmail == studentUser.Email)), ct);

        var studentName = studentUser?.DisplayName ?? (studentProfile != null ? $"{studentProfile.FirstName} {studentProfile.LastName}" : "Unknown Student");
        var matricNumber = studentProfile?.StudentNumber;

        var detail = new StudentAutoRegistrationDetailDto
        {
            StudentId = studentId,
            StudentName = studentName,
            MatricNumber = matricNumber
        };

        var config = await _context.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new SystemRegistrationConfiguration();

        var session = await _context.AcademicSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == academicSessionId, ct);
        if (session is null)
        {
            detail.Status = "Skipped";
            detail.Message = "Target academic session not found.";
            return detail;
        }

        var enrollment = await ResolveProgrammeEnrollmentAsync(studentId, academicSessionId, ct);
        if (enrollment is null)
        {
            detail.Status = "Skipped";
            detail.Message = "Student is not enrolled in an academic program for this session.";
            return detail;
        }

        var program = await _context.Programs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == enrollment.ProgramId, ct);
        var level = await _context.Levels.AsNoTracking().FirstOrDefaultAsync(l => l.Id == enrollment.LevelId, ct);

        detail.ProgramName = program?.Name ?? "Unknown Program";
        detail.LevelName = level?.Name ?? "Unknown Level";

        // Level scope filtering
        if (string.Equals(config.AutoRegisterTargetLevels, "100LevelOnly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(config.AutoRegisterTargetLevels, "100", StringComparison.OrdinalIgnoreCase))
        {
            bool is100Level = (level != null && (level.Order == 1 || (level.Name != null && level.Name.Contains("100"))));
            if (!is100Level)
            {
                detail.Status = "Skipped";
                detail.Message = $"Auto-registration configured for 100 Level only (student is in {detail.LevelName}).";
                return detail;
            }
        }

        // Resolve curriculum
        var curriculumId = enrollment.CurriculumId != Guid.Empty
            ? enrollment.CurriculumId
            : await _context.Curricula.AsNoTracking()
                .Where(c => c.ProgramId == enrollment.ProgramId && c.IsActive)
                .OrderByDescending(c => c.CreatedUtc)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

        if (curriculumId == Guid.Empty)
        {
            detail.Status = "Skipped";
            detail.Message = "No active curriculum found for the student's program.";
            return detail;
        }

        var curriculumCourses = await _context.CurriculumCourses.AsNoTracking()
            .Where(cc => cc.CurriculumId == curriculumId)
            .Include(cc => cc.Course)
            .ToListAsync(ct);

        // Grade totals to check passed courses
        var gradeGroupTotals = await _context.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId &&
                _context.GradePublications.Any(p => p.CourseOfferingId == g.Assessment.CourseOfferingId && p.IsVisibleToStudents))
            .GroupBy(g => new { g.Assessment.CourseOffering.CourseId, g.Assessment.CourseOfferingId })
            .Select(g => new { g.Key.CourseId, TotalMax = g.Sum(x => x.Assessment.MaxMarks), TotalObtained = g.Sum(x => x.MarksObtained) })
            .ToListAsync(ct);
        var passedCourseIds = gradeGroupTotals
            .Where(g => g.TotalMax > 0 && g.TotalObtained / g.TotalMax * 100m >= 40m)
            .Select(g => g.CourseId)
            .ToHashSet();

        List<Semester> targetSemesters;
        if (targetSemester.HasValue)
        {
            targetSemesters = new List<Semester> { targetSemester.Value };
        }
        else if (config.AllowMultiSemesterRegistration)
        {
            targetSemesters = new List<Semester> { Semester.First, Semester.Second };
        }
        else
        {
            targetSemesters = new List<Semester> { session.ActiveSemester };
        }

        var candidateOfferingsToRegister = new List<(CourseOffering offering, int credits, string code, bool isCarryover)>();

        foreach (var sem in targetSemesters)
        {
            // Max credits config for this semester
            var maxCredits = await _context.LevelSemesterConfigs.AsNoTracking()
                .Where(x => x.LevelId == enrollment.LevelId && x.Semester == sem && x.IsActive)
                .Select(x => (int?)x.MaxCreditLoad)
                .FirstOrDefaultAsync(ct) ?? 24;

            // Current registered credits for this semester
            var existingRegistrations = await _context.CourseEnrollments.AsNoTracking()
                .Where(e => e.StudentId == studentId && e.Status == "Registered" &&
                            e.CourseOffering.AcademicSessionId == academicSessionId &&
                            e.CourseOffering.Semester == sem)
                .Include(e => e.CourseOffering)
                    .ThenInclude(co => co.Course)
                .ToListAsync(ct);

            var registeredOfferingIds = existingRegistrations.Select(e => e.CourseOfferingId).ToHashSet();
            var registeredCourseIds = existingRegistrations.Select(e => e.CourseOffering.CourseId).ToHashSet();
            var currentCredits = existingRegistrations.Sum(e => e.CourseOffering?.Course?.CreditUnits ?? 0);

            // 1. Handle Carryovers first if configured
            if (config.AutoRegisterCarryovers)
            {
                var levels = await _context.Levels.AsNoTracking()
                    .Where(x => x.ProgramId == enrollment.ProgramId)
                    .ToListAsync(ct);
                var currentLevelOrder = level?.Order ?? 1;
                var lowerLevelIds = levels.Where(x => x.Order < currentLevelOrder).Select(x => x.Id).ToList();

                if (lowerLevelIds.Count > 0)
                {
                    var carryoverOfferings = await _context.CourseOfferings.AsNoTracking()
                        .Where(x => x.AcademicSessionId == academicSessionId &&
                                    x.Semester == sem &&
                                    _context.CourseOfferingPrograms.Any(p =>
                                        p.CourseOfferingId == x.Id &&
                                        p.ProgramId == enrollment.ProgramId &&
                                        lowerLevelIds.Contains(p.LevelId)))
                        .Include(x => x.Course)
                        .ToListAsync(ct);

                    foreach (var co in carryoverOfferings)
                    {
                        if (passedCourseIds.Contains(co.CourseId)) continue;
                        if (registeredOfferingIds.Contains(co.Id) || registeredCourseIds.Contains(co.CourseId) || candidateOfferingsToRegister.Any(c => c.offering.Id == co.Id || c.offering.CourseId == co.CourseId))
                        {
                            detail.SkippedCourses.Add($"{co.Course?.Code ?? "UNKNOWN"} (Carryover already registered)");
                            continue;
                        }
                        if (co.IsRegistrationClosed)
                        {
                            detail.SkippedCourses.Add($"{co.Course?.Code ?? "UNKNOWN"} (Offering closed)");
                            continue;
                        }

                        var courseCredits = co.Course?.CreditUnits ?? 0;
                        if (string.Equals(config.AutoRegisterCreditLimitHandling, "Strict", StringComparison.OrdinalIgnoreCase) &&
                            currentCredits + courseCredits > maxCredits)
                        {
                            detail.SkippedCourses.Add($"{co.Course?.Code ?? "UNKNOWN"} (Carryover exceeds {maxCredits} max credit limit for {sem} semester)");
                            continue;
                        }

                        candidateOfferingsToRegister.Add((co, courseCredits, co.Course?.Code ?? "UNKNOWN", true));
                        currentCredits += courseCredits;
                        registeredCourseIds.Add(co.CourseId);
                    }
                }
            }

            // 2. Handle Current Level Curriculum Courses
            var currentLevelCourses = curriculumCourses
                .Where(cc => cc.LevelId == enrollment.LevelId && cc.Semester == sem);

            if (!string.IsNullOrEmpty(config.AutoRegisterCourseCategories) &&
                (string.Equals(config.AutoRegisterCourseCategories, "Compulsory", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(config.AutoRegisterCourseCategories, "CompulsoryOnly", StringComparison.OrdinalIgnoreCase)))
            {
                currentLevelCourses = currentLevelCourses.Where(cc => cc.Category == CourseCategory.Compulsory);
            }

            var sortedCurriculumCourses = currentLevelCourses
                .OrderBy(cc => cc.Category == CourseCategory.Compulsory ? 0 : 1)
                .ThenBy(cc => cc.Course?.Code)
                .ToList();

            foreach (var cc in sortedCurriculumCourses)
            {
                if (cc.Course == null) continue;

                if (passedCourseIds.Contains(cc.CourseId))
                {
                    detail.SkippedCourses.Add($"{cc.Course.Code} (Already passed)");
                    continue;
                }

                if (registeredCourseIds.Contains(cc.CourseId) || candidateOfferingsToRegister.Any(c => c.offering.CourseId == cc.CourseId))
                {
                    detail.SkippedCourses.Add($"{cc.Course.Code} (Already registered)");
                    continue;
                }

                // Find offering
                var offering = await _context.CourseOfferings.AsNoTracking()
                    .FirstOrDefaultAsync(co => co.CourseId == cc.CourseId &&
                                               co.AcademicSessionId == academicSessionId &&
                                               co.Semester == sem, ct);

                if (offering is null)
                {
                    detail.SkippedCourses.Add($"{cc.Course.Code} (No offering scheduled for {sem} semester)");
                    continue;
                }

                if (offering.IsRegistrationClosed)
                {
                    detail.SkippedCourses.Add($"{cc.Course.Code} (Offering closed for registration)");
                    continue;
                }

                var courseCredits = cc.CreditUnits > 0 ? cc.CreditUnits : (offering.Course?.CreditUnits ?? 0);
                if (string.Equals(config.AutoRegisterCreditLimitHandling, "Strict", StringComparison.OrdinalIgnoreCase) &&
                    currentCredits + courseCredits > maxCredits)
                {
                    detail.SkippedCourses.Add($"{cc.Course.Code} (Exceeds {maxCredits} max credits limit for {sem} semester)");
                    continue;
                }

                candidateOfferingsToRegister.Add((offering, courseCredits, cc.Course.Code, false));
                currentCredits += courseCredits;
                registeredCourseIds.Add(cc.CourseId);
            }
        }

        // Commit or dry-run
        if (candidateOfferingsToRegister.Count > 0)
        {
            if (!isDryRun)
            {
                var now = DateTime.UtcNow;
                foreach (var (offering, credits, code, isCarryover) in candidateOfferingsToRegister)
                {
                    var newEnrollment = new CourseEnrollment
                    {
                        Id = Guid.NewGuid(),
                        StudentId = studentId,
                        CourseOfferingId = offering.Id,
                        Status = "Registered",
                        RegisteredAtUtc = now,
                        CreatedById = studentId
                    };
                    _context.CourseEnrollments.Add(newEnrollment);
                    detail.RegisteredCourses.Add(isCarryover ? $"{code} [Carryover]" : code);
                }

                await _context.SaveChangesAsync(ct);

                await LogActionAsync(
                    "AutoRegisterCourses",
                    "Student",
                    studentId.ToString(),
                    $"Auto-registered {candidateOfferingsToRegister.Count} courses ({candidateOfferingsToRegister.Sum(x => x.credits)} credits) for session {session.Name}",
                    ct);
            }
            else
            {
                foreach (var (_, _, code, isCarryover) in candidateOfferingsToRegister)
                {
                    detail.RegisteredCourses.Add(isCarryover ? $"{code} [Carryover]" : code);
                }
            }

            detail.CoursesRegisteredCount = candidateOfferingsToRegister.Count;
            detail.TotalCredits = candidateOfferingsToRegister.Sum(x => x.credits);
            detail.Status = "Success";
            detail.Message = isDryRun
                ? $"Eligible for auto-registration: {detail.CoursesRegisteredCount} course(s), {detail.TotalCredits} credits."
                : $"Successfully auto-registered {detail.CoursesRegisteredCount} course(s), {detail.TotalCredits} credits.";
        }
        else
        {
            detail.Status = "Skipped";
            detail.Message = detail.SkippedCourses.Count > 0
                ? "No new eligible courses to register (all candidate courses were skipped)."
                : "No candidate curriculum courses found for this level and semester.";
        }

        return detail;
    }

    public async Task<BatchAutoRegistrationResultDto> RunBatchAutoRegistrationAsync(
        BatchAutoRegistrationRequest request,
        CancellationToken ct = default)
    {
        var result = new BatchAutoRegistrationResultDto
        {
            DryRun = request.DryRun
        };

        var config = await _context.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new SystemRegistrationConfiguration();

        var session = await _context.AcademicSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.AcademicSessionId, ct);
        if (session == null)
        {
            result.Logs.Add($"Error: Target session {request.AcademicSessionId} not found.");
            return result;
        }

        var targetInfo = request.TargetSemester.HasValue
            ? $"Target Semester: {request.TargetSemester.Value}"
            : (config.AllowMultiSemesterRegistration ? "Multi-Semester (First & Second Semesters)" : $"Active Semester: {session.ActiveSemester}");

        result.Logs.Add($"Starting {(request.DryRun ? "DRY-RUN " : "")}batch auto-registration for session '{session.Name}' ({targetInfo})...");

        // Find candidate student enrollments in this session
        var query = _context.Enrollments.AsNoTracking()
            .Where(e => e.AcademicSessionId == request.AcademicSessionId);

        if (request.ProgramId.HasValue && request.ProgramId.Value != Guid.Empty)
        {
            query = query.Where(e => e.ProgramId == request.ProgramId.Value);
        }

        if (request.LevelId.HasValue && request.LevelId.Value != Guid.Empty)
        {
            query = query.Where(e => e.LevelId == request.LevelId.Value);
        }

        var studentIds = await query.Select(e => e.UserId).Distinct().ToListAsync(ct);
        result.TotalStudentsEvaluated = studentIds.Count;

        result.Logs.Add($"Found {studentIds.Count} enrolled student(s) matching criteria.");

        foreach (var studentId in studentIds)
        {
            try
            {
                var studentDetail = await AutoRegisterStudentAsync(studentId, request.AcademicSessionId, request.DryRun, request.TargetSemester, ct);
                result.Details.Add(studentDetail);

                if (studentDetail.Status == "Success")
                {
                    result.SuccessCount++;
                    result.TotalCoursesRegistered += studentDetail.CoursesRegisteredCount;
                    result.Logs.Add($"[OK] {studentDetail.StudentName} ({studentDetail.MatricNumber ?? "No Matric"}): Enrolled in {studentDetail.CoursesRegisteredCount} course(s) ({string.Join(", ", studentDetail.RegisteredCourses)})");
                }
                else
                {
                    result.SkippedCount++;
                    result.Logs.Add($"[SKIPPED] {studentDetail.StudentName}: {studentDetail.Message}");
                }
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                _logger.LogError(ex, "Error during auto-registration for student {StudentId}", studentId);
                result.Details.Add(new StudentAutoRegistrationDetailDto
                {
                    StudentId = studentId,
                    Status = "Failed",
                    Message = ex.Message
                });
                result.Logs.Add($"[ERROR] Student ID {studentId}: {ex.Message}");
            }
        }

        result.Logs.Add($"Completed batch auto-registration. Total Evaluated: {result.TotalStudentsEvaluated}, Success: {result.SuccessCount}, Skipped: {result.SkippedCount}, Failed: {result.FailedCount}, Total Courses Enrolled: {result.TotalCoursesRegistered}.");

        return result;
    }

    private async Task<ProgramEnrollment?> ResolveProgrammeEnrollmentAsync(Guid studentId, Guid academicSessionId, CancellationToken ct)
    {
        var programmeEnrollment = await _context.Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == studentId && x.AcademicSessionId == academicSessionId, ct);

        if (programmeEnrollment != null)
        {
            return programmeEnrollment;
        }

        var prevEnrollment = await _context.Enrollments.AsNoTracking()
            .Where(x => x.UserId == studentId)
            .OrderByDescending(x => x.EnrolledAtUtc)
            .FirstOrDefaultAsync(ct);

        var student = await _context.Set<Student>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId || (s.EntraObjectId != null
                ? _context.Users.Any(u => u.Id == studentId && u.EntraObjectId == s.EntraObjectId)
                : _context.Users.Any(u => u.Id == studentId && u.Email == s.OfficialEmail)), ct)
            ?? await _context.Set<Student>().AsNoTracking()
                .Where(s => _context.Users.Any(u => u.Id == studentId && (u.Email == s.OfficialEmail || u.Email == s.PersonalEmail)))
                .FirstOrDefaultAsync(ct);

        Guid? programId = prevEnrollment?.ProgramId ?? student?.AcademicProgramId;
        Guid? levelId = prevEnrollment?.LevelId ?? student?.LevelId;
        Guid curriculumId = prevEnrollment?.CurriculumId ?? Guid.Empty;

        if (programId.HasValue && !levelId.HasValue)
        {
            var defaultLevel = await _context.Levels.AsNoTracking()
                .Where(l => l.ProgramId == programId.Value)
                .OrderBy(l => l.Order)
                .FirstOrDefaultAsync(ct);
            levelId = defaultLevel?.Id;
        }

        if (programId.HasValue && levelId.HasValue)
        {
            if (curriculumId == Guid.Empty)
            {
                curriculumId = await _context.Curricula.AsNoTracking()
                    .Where(c => c.ProgramId == programId.Value && c.IsActive)
                    .OrderByDescending(c => c.CreatedUtc)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync(ct);
            }

            var newEnrollment = new ProgramEnrollment
            {
                Id = Guid.NewGuid(),
                UserId = studentId,
                AcademicSessionId = academicSessionId,
                ProgramId = programId.Value,
                LevelId = levelId.Value,
                CurriculumId = curriculumId,
                EnrolledAtUtc = DateTime.UtcNow
            };

            _context.Enrollments.Add(newEnrollment);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch
            {
                // Ignore concurrent save conflicts if created simultaneously
            }

            return newEnrollment;
        }

        return null;
    }
}
