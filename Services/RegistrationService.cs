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

public class RegistrationService : BaseService, IRegistrationService
{
    private readonly LmsDbContext _context;

    public RegistrationService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<CourseRegistrationDto>> RegisterStudent(Guid studentId, Guid courseOfferingId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty || courseOfferingId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID and Course Offering ID must be provided.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<ErrorOr<CourseRegistrationDto>>(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var offering = await LoadOfferingAsync(courseOfferingId, ct);
            if (offering is null)
                return Error.NotFound("CourseOffering.NotFound", "Course offering not found.");

            if (await IsRegistrationLockedAsync(studentId, offering.AcademicSessionId, ct))
                return Error.Conflict("Registration.VerifiedLocked", "Your registration has been verified by your course adviser and can no longer be changed by you.");

            var blockers = await GetBlockersAsync(studentId, offering, ct);
            var hardBlockers = blockers.Where(b => b.Code != "Registration.IneligibleOffering" &&
                                                  b.Code != "Registration.NotInCurriculum" &&
                                                  b.Code != "Registration.WrongSemester").ToList();
            if (hardBlockers.Count > 0)
                return Error.Validation(hardBlockers[0].Code, hardBlockers[0].Message);

            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.CourseOfferingId == courseOfferingId, ct);
            if (enrollment is null)
            {
                enrollment = new CourseEnrollment
                {
                    StudentId = studentId,
                    CourseOfferingId = courseOfferingId,
                    CreatedById = studentId
                };
                _context.CourseEnrollments.Add(enrollment);
            }
            else
            {
                enrollment.Status = "Registered";
                enrollment.RegisteredAtUtc = DateTime.UtcNow;
                enrollment.DroppedAtUtc = null;
                enrollment.UpdatedById = studentId;
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await LogActionAsync("RegisterStudent", "CourseEnrollment", enrollment.Id.ToString(),
                $"Student {studentId} registered for offering {courseOfferingId}", ct);
            return MapRegistration(enrollment, offering, await GetCurriculumCreditsAsync(studentId, offering.CourseId, ct));
        });
    }

    public async Task<ErrorOr<Deleted>> DropCourse(Guid studentId, Guid enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await _context.CourseEnrollments
            .Include(x => x.CourseOffering)
                .ThenInclude(o => o.AcademicSession)
            .FirstOrDefaultAsync(x => x.Id == enrollmentId && x.StudentId == studentId, ct);
        if (enrollment == null)
            return Error.NotFound("Enrollment.NotFound", "Enrollment record not found.");

        if (enrollment.Status == "Dropped")
            return Error.Conflict("Registration.AlreadyDropped", "This course has already been dropped.");

        if (enrollment.CourseOffering == null)
            return Error.NotFound("CourseOffering.NotFound", "Associated course offering not found.");

        var session = enrollment.CourseOffering.AcademicSession;
        if (session == null || !session.IsActive || DateTime.UtcNow > session.EndDate)
            return Error.Conflict("Registration.SemesterEnded", "You cannot drop a course after the academic session has ended.");

        if (await IsRegistrationLockedAsync(studentId, session.Id, ct))
            return Error.Conflict("Registration.VerifiedLocked", "Your registration has been verified by your course adviser and can no longer be changed by you.");

        var isPublished = await _context.GradePublications
            .AnyAsync(x => x.CourseOfferingId == enrollment.CourseOfferingId && x.IsVisibleToStudents, ct);
        if (isPublished)
            return Error.Conflict("Registration.GradesPublished", "You cannot drop a course once its results have been published.");

        enrollment.Status = "Dropped";
        enrollment.DroppedAtUtc = DateTime.UtcNow;
        enrollment.UpdatedById = studentId;
        await _context.SaveChangesAsync(ct);
        await LogActionAsync("DropCourse", "CourseEnrollment", enrollment.Id.ToString(),
            $"Student {studentId} dropped offering {enrollment.CourseOfferingId}", ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<CourseSwapRequestDto>> RequestCourseSwapAsync(Guid studentId, Guid currentCourseOfferingId, Guid newCourseOfferingId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty || currentCourseOfferingId == Guid.Empty || newCourseOfferingId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID, current course offering ID, and new course offering ID must be provided.");
        }

        if (currentCourseOfferingId == newCourseOfferingId)
        {
            return Error.Validation("InvalidInput", "Current course offering and new course offering must be different.");
        }

        var currentOffering = await _context.CourseOfferings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == currentCourseOfferingId, ct);
        if (currentOffering is null)
            return Error.NotFound("CourseOffering.NotFound", "Current course offering not found.");

        if (await IsRegistrationLockedAsync(studentId, currentOffering.AcademicSessionId, ct))
            return Error.Conflict("Registration.VerifiedLocked", "Your registration has been verified by your course adviser and can no longer be changed by you.");

        // Verify student is enrolled in the current course offering
        var currentProgramId = await GetProgramIdFromOffering(currentCourseOfferingId, ct);
        var currentEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == studentId && e.ProgramId == currentProgramId, ct);

        if (currentEnrollment == null)
        {
            return Error.NotFound("Enrollment.NotFound", "Student is not enrolled in the current course offering.");
        }

        var newProgramId = await GetProgramIdFromOffering(newCourseOfferingId, ct);
        var newEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == studentId && e.ProgramId == newProgramId, ct);

        if (newEnrollment != null)
        {
            return Error.Conflict("AlreadyEnrolled", "Student is already enrolled in the new course offering.");
        }

        // Check if there's already a pending swap request for this student and course combination
        var existingSwapRequest = await _context.CourseSwapRequests
            .FirstOrDefaultAsync(r => 
                r.StudentId == studentId && 
                r.CourseOfferingToDropId == currentCourseOfferingId && 
                r.CourseOfferingToAddId == newCourseOfferingId && 
                r.Status == "Pending", ct);

        if (existingSwapRequest != null)
        {
            return Error.Conflict("SwapRequestExists", "A swap request for this course combination already exists.");
        }

        var swapRequest = new CourseSwapRequest
        {
            StudentId = studentId,
            CourseOfferingToDropId = currentCourseOfferingId,
            CourseOfferingToAddId = newCourseOfferingId,
            Status = "Pending",
            RequestedAtUtc = DateTime.UtcNow,
            CreatedById = studentId,
            CreatedByUserId = studentId
        };

        _context.CourseSwapRequests.Add(swapRequest);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("RequestCourseSwap", "CourseSwapRequest", swapRequest.Id.ToString(),
            $"Student {studentId} requested to swap from course offering {currentCourseOfferingId} to {newCourseOfferingId}", ct);

        var swapStudentName = (swapRequest.Student?.FirstName + " " + swapRequest.Student?.LastName) ?? "Unknown Student";
        var swapProcessedByName = swapRequest.ProcessedById.HasValue ? (swapRequest.ProcessedBy?.DisplayName ?? swapRequest.ProcessedBy?.Email ?? "Unknown") : null;
        return new CourseSwapRequestDto(
            swapRequest.Id,
            swapRequest.StudentId,
            swapStudentName,
            swapRequest.CourseOfferingToDropId,
            swapRequest.CourseOfferingToDrop?.Course?.Code ?? "Unknown",
            swapRequest.CourseOfferingToAddId,
            swapRequest.CourseOfferingToAdd?.Course?.Code ?? "Unknown",
            swapRequest.Status,
            swapRequest.RequestedAtUtc,
            swapRequest.ProcessedAtUtc,
            swapProcessedByName,
            swapRequest.RejectionReason);
    }

    public async Task<ErrorOr<CourseSwapOptionsDto>> GetCourseSwapOptionsAsync(Guid studentId, CancellationToken ct = default)
    {
        var enrollments = await _context.Enrollments
            .Where(enrollment => enrollment.UserId == studentId)
            .ToListAsync(ct);

        if (enrollments.Count == 0)
        {
            return Error.NotFound("Enrollment.NotFound", "No active programme enrollment was found for this student.");
        }

        var session = await _context.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct);
        if (session is null)
            return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");

        var programIds = enrollments.Select(enrollment => enrollment.ProgramId).Distinct().ToList();
        var levelIds = enrollments.Select(enrollment => enrollment.LevelId).Distinct().ToList();

        var offerings = await _context.CourseOfferings
            .AsNoTracking()
            .Include(offering => offering.Course)
            .Include(offering => offering.AcademicSession)
            .Where(offering =>
                offering.AcademicSessionId == session.Id &&
                offering.Semester == session.ActiveSemester &&
                _context.CourseOfferingPrograms.Any(p =>
                    p.CourseOfferingId == offering.Id && levelIds.Contains(p.LevelId)))
            .OrderBy(offering => offering.Course.Code)
            .ToListAsync(ct);

        static CourseSwapOptionDto MapOption(CourseOffering offering) => new(
            offering.Id,
            offering.Course?.Code ?? "Unknown",
            offering.Course?.Title ?? "Untitled course",
            offering.AcademicSession?.Name ?? "Unknown session");

        var currentCourses = offerings
            .Where(offering => _context.CourseOfferingPrograms
                .Any(p => p.CourseOfferingId == offering.Id && programIds.Contains(p.ProgramId)))
            .Select(MapOption)
            .ToList();
        var availableCourses = offerings
            .Where(offering => !_context.CourseOfferingPrograms
                .Any(p => p.CourseOfferingId == offering.Id && programIds.Contains(p.ProgramId)))
            .Select(MapOption)
            .ToList();

        return new CourseSwapOptionsDto(currentCourses, availableCourses);
    }

    public async Task<ErrorOr<List<CourseSwapRequestDto>>> GetSwapRequestsAsync(Guid? studentId = null, CancellationToken ct = default)
    {
        var query = _context.CourseSwapRequests
            .Include(r => r.Student)
            .Include(r => r.CourseOfferingToDrop)
                .ThenInclude(co => co!.Course)
            .Include(r => r.CourseOfferingToAdd)
                .ThenInclude(co => co!.Course)
            .Include(r => r.ProcessedBy)
            .AsQueryable();

        if (studentId.HasValue)
        {
            query = query.Where(request => request.StudentId == studentId.Value);
        }

        var swapRequests = await query.OrderByDescending(request => request.RequestedAtUtc).ToListAsync(ct);

        return swapRequests.Select(r =>
        {
            var name = (r.Student?.FirstName + " " + r.Student?.LastName) ?? "Unknown Student";
            var processedByName = r.ProcessedById.HasValue ? (r.ProcessedBy?.DisplayName ?? r.ProcessedBy?.Email ?? "Unknown") : null;
            return new CourseSwapRequestDto(
                r.Id,
                r.StudentId,
                name,
                r.CourseOfferingToDropId,
                r.CourseOfferingToDrop?.Course?.Code ?? "Unknown",
                r.CourseOfferingToAddId,
                r.CourseOfferingToAdd?.Course?.Code ?? "Unknown",
                r.Status,
                r.RequestedAtUtc,
                r.ProcessedAtUtc,
                processedByName,
                r.RejectionReason);
        }).ToList();
    }

    public async Task<ErrorOr<Deleted>> ProcessSwapRequestAsync(Guid requestId, bool approved, string? adminNotes, CancellationToken ct = default)
    {
        var swapRequest = await _context.CourseSwapRequests.FindAsync(new object[] { requestId }, ct);
        if (swapRequest == null)
        {
            return Error.NotFound("CourseSwapRequest.NotFound", "Swap request not found.");
        }

        if (swapRequest.Status != "Pending")
        {
            return Error.Validation("InvalidStatus", "Only pending swap requests can be processed.");
        }

        swapRequest.Status = approved ? "Approved" : "Rejected";
        swapRequest.ProcessedAtUtc = DateTime.UtcNow;
        swapRequest.ProcessedById = Guid.Empty; // In a real implementation, this would be the admin user's ID
        swapRequest.RejectionReason = approved ? null : adminNotes;

        if (approved)
        {
            // If approved, drop the current course and add the new one
            var dropProgramId = await GetProgramIdFromOffering(swapRequest.CourseOfferingToDropId, ct);
            var currentEnrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == swapRequest.StudentId && e.ProgramId == dropProgramId, ct);

            if (currentEnrollment != null)
            {
                _context.Enrollments.Remove(currentEnrollment);
            }

            var newCourseOffering = await _context.CourseOfferings
                .Include(co => co.Programs).ThenInclude(p => p.Program)
                .Include(co => co.Programs).ThenInclude(p => p.Level)
                .Include(co => co.AcademicSession)
                .FirstOrDefaultAsync(co => co.Id == swapRequest.CourseOfferingToAddId, ct);

            if (newCourseOffering != null)
            {
                var firstProgram = newCourseOffering.Programs.FirstOrDefault();
                if (firstProgram != null)
                {
                    var newEnrollment = new ProgramEnrollment
                    {
                        ProgramId         = firstProgram.ProgramId,
                        LevelId           = firstProgram.LevelId,
                        UserId            = swapRequest.StudentId,
                        AcademicSessionId = newCourseOffering.AcademicSessionId,
                        CurriculumId      = Guid.Empty,
                        EnrolledAtUtc     = DateTime.UtcNow
                    };
                    _context.Enrollments.Add(newEnrollment);
                }
            }
        }

        await _context.SaveChangesAsync(ct);

        await LogActionAsync(approved ? "ApproveSwapRequest" : "RejectSwapRequest", "CourseSwapRequest", requestId.ToString(),
            $"Swap request {requestId} was {(approved ? "approved" : "rejected")}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<List<CourseRegistrationDto>>> GetRegistrationHistoryAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var baseQuery = _context.CourseEnrollments.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Include(x => x.CourseOffering).ThenInclude(x => x.Course)
            .OrderByDescending(x => x.RegisteredAtUtc);

        IQueryable<LMS.Api.Data.Entities.CourseEnrollment> filteredQuery = academicSessionId.HasValue
            ? baseQuery.Where(x => x.CourseOffering.AcademicSessionId == academicSessionId.Value)
            : baseQuery;

        return await filteredQuery
            .Select(x => new CourseRegistrationDto(x.Id, x.StudentId, x.CourseOfferingId,
                x.CourseOffering.Course.Code, x.CourseOffering.Course.Title, x.RegisteredAtUtc,
                x.DroppedAtUtc, x.Status, x.CourseOffering.Course.CreditUnits))
            .ToListAsync(ct);
    }

    public async Task<ErrorOr<RegistrationSummaryDto>> GetRegistrationSummaryAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        AcademicSession? session;
        if (academicSessionId.HasValue)
        {
            session = await _context.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == academicSessionId.Value, ct);
            if (session is null)
                return Error.NotFound("AcademicSession.NotFound", "The specified academic session was not found.");
        }
        else
        {
            session = await _context.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct);
            if (session is null)
                return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");
        }

        var programmeEnrollment = await ResolveProgrammeEnrollmentAsync(studentId, session.Id, ct);

        var studentName = await _context.Users.Where(x => x.Id == studentId)
            .Select(x => x.DisplayName ?? x.Email ?? "Student").FirstOrDefaultAsync(ct) ?? "Student";

        var programName = "";
        var levelName = "";
        if (programmeEnrollment is not null)
        {
            var program = await _context.Programs.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == programmeEnrollment.ProgramId, ct);
            var level = await _context.Levels.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == programmeEnrollment.LevelId, ct);
            programName = program?.Name ?? "";
            levelName = level?.Name ?? "";
        }
        else
        {
            var student = await _context.Set<Student>().AsNoTracking()
                .Include(s => s.AcademicProgram)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.Id == studentId || (s.EntraObjectId != null
                    ? _context.Users.Any(u => u.Id == studentId && u.EntraObjectId == s.EntraObjectId)
                    : _context.Users.Any(u => u.Id == studentId && u.Email == s.OfficialEmail)), ct)
                ?? await _context.Set<Student>().AsNoTracking()
                    .Include(s => s.AcademicProgram)
                    .Include(s => s.Level)
                    .Where(s => _context.Users.Any(u => u.Id == studentId && (u.Email == s.OfficialEmail || u.Email == s.PersonalEmail)))
                    .FirstOrDefaultAsync(ct);

            if (student is not null)
            {
                programName = student.AcademicProgram?.Name ?? "";
                levelName = student.Level?.Name ?? "";
            }
        }

        // If no programme enrollment exists for the active session, return an empty summary
        if (programmeEnrollment is null)
        {
            var emptyOfferings = new List<CourseOffering>();

            var emptySlots = await _context.LectureTimetableSlots.AsNoTracking()
                .Where(x => emptyOfferings.Select(o => o.Id).Contains(x.CourseOfferingId))
                .OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime)
                .ToListAsync(ct);

            var emptyOptionDtos = new List<RegistrationOfferingDto>();
            foreach (var offering in emptyOfferings)
            {
                var blockers = await GetBlockersAsync(studentId, offering, ct);
                emptyOptionDtos.Add(new RegistrationOfferingDto(
                    offering.Id, offering.Course?.Code ?? string.Empty, offering.Course?.Title ?? string.Empty, offering.Course?.CreditUnits ?? 0,
                    (int)offering.Semester, "To be announced",
                    emptySlots.Where(s => s.CourseOfferingId == offering.Id)
                        .Select(s => $"{s.DayOfWeek} {s.StartTime:HH\\:mm}–{s.EndTime:HH\\:mm}").ToList(),
                    false, false, blockers));
            }

            var configEmpty = await _context.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
                ?? new SystemRegistrationConfiguration { Strategy = "Single", EnforceMinCredits = true };

            var emptyVerification = await GetRegistrationVerificationAsync(studentId, session.Id, ct);
            return new RegistrationSummaryDto(studentId, studentName, session.Id, session.Name,
                0, 0, new List<CourseRegistrationDto>(), emptyOptionDtos, programName, levelName,
                configEmpty.Strategy, 0, emptyVerification is not null, emptyVerification?.VerifiedAtUtc);
        }

        var levels = await _context.Levels.AsNoTracking()
            .Where(x => x.ProgramId == programmeEnrollment.ProgramId)
            .ToListAsync(ct);
        var currentLevel = levels.FirstOrDefault(x => x.Id == programmeEnrollment.LevelId);
        var lowerLevelIds = currentLevel != null 
            ? levels.Where(x => x.Order < currentLevel.Order).Select(x => x.Id).ToList() 
            : new List<Guid>();

        // Query published grades to identify passed courses
        var allGradeRows = await _context.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId &&
                _context.GradePublications.Any(p => p.CourseOfferingId == g.Assessment.CourseOfferingId && p.IsVisibleToStudents))
            .Select(g => new { g.Assessment.CourseOffering.CourseId, g.Assessment.CourseOfferingId, g.MarksObtained, g.Assessment.MaxMarks })
            .ToListAsync(ct);

        var passedCourseIds = allGradeRows.GroupBy(x => new { x.CourseId, x.CourseOfferingId })
            .Where(g => g.Sum(x => x.MaxMarks) > 0 && g.Sum(x => x.MarksObtained) / g.Sum(x => x.MaxMarks) * 100m >= 40m)
            .Select(g => g.Key.CourseId).ToHashSet();

        // Load curriculum maps — resolved by admission session, then program active curriculum
        var resolvedCurriculumId = await ResolveCurriculumIdAsync(studentId, programmeEnrollment, ct);

        var curriculumCreditMap = new Dictionary<Guid, int>();
        var curriculumCourseSemesters = new Dictionary<Guid, LMS.Api.Data.Enums.Semester>();
        var curriculumCourseLevels = new Dictionary<Guid, Guid>();
        if (resolvedCurriculumId != Guid.Empty)
        {
            var ccList = await _context.CurriculumCourses.AsNoTracking()
                .Where(x => x.CurriculumId == resolvedCurriculumId)
                .ToListAsync(ct);
            var distinctCCs = ccList.GroupBy(x => x.CourseId).Select(g => g.First()).ToList();
            curriculumCreditMap = distinctCCs.ToDictionary(x => x.CourseId, x => x.CreditUnits);
            curriculumCourseSemesters = distinctCCs.ToDictionary(x => x.CourseId, x => x.Semester);
            curriculumCourseLevels = distinctCCs.ToDictionary(x => x.CourseId, x => x.LevelId);
        }

        var offerings = await _context.CourseOfferings.AsNoTracking()
            .Where(x => x.AcademicSessionId == session.Id &&
                        x.Semester == session.ActiveSemester &&
                        _context.CourseOfferingPrograms.Any(p =>
                            p.CourseOfferingId == x.Id &&
                            p.ProgramId == programmeEnrollment.ProgramId &&
                            (p.LevelId == programmeEnrollment.LevelId || lowerLevelIds.Contains(p.LevelId))))
            .Include(x => x.Course)
            .Include(x => x.AcademicSession)
            .Include(x => x.Programs).ThenInclude(p => p.Level)
            .Include(x => x.Lecturers).ThenInclude(l => l.Lecturer)
            .OrderBy(x => x.Course.Code).ToListAsync(ct);

        // Hard-filter: never show offerings whose level order exceeds the student's level order
        var studentLevelOrder = currentLevel?.Order ?? 0;
        offerings = offerings.Where(x =>
            x.Programs.All(p => p.Level == null || p.Level.Order <= studentLevelOrder) ||
            !x.Programs.Any()).ToList();

        // Enforce curriculum mapping, semester, and level — only if the curriculum has courses for this student's context
        // If the curriculum exists but has no courses for this level/semester, fall back gracefully
        if (resolvedCurriculumId != Guid.Empty && curriculumCreditMap.Count > 0)
        {
            var curriculumFiltered = offerings.Where(x => 
                curriculumCreditMap.ContainsKey(x.CourseId) && 
                curriculumCourseSemesters.TryGetValue(x.CourseId, out var sem) && 
                sem == session.ActiveSemester &&
                curriculumCourseLevels.TryGetValue(x.CourseId, out var lvl) && 
                (lvl == programmeEnrollment.LevelId || lowerLevelIds.Contains(lvl))
            ).ToList();

            // Only apply curriculum filter if it returns results; otherwise keep level+semester filtered offerings
            if (curriculumFiltered.Count > 0)
                offerings = curriculumFiltered;
        }

        // Filter out lower-level offerings if the student has already passed them
        offerings = offerings
            .Where(x => !x.Programs.Any(p => lowerLevelIds.Contains(p.LevelId)) || !passedCourseIds.Contains(x.CourseId))
            .ToList();

        var offeringIds = offerings.Select(x => x.Id).ToList();
        var slots = await _context.LectureTimetableSlots.AsNoTracking()
            .Where(x => offeringIds.Contains(x.CourseOfferingId)).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).ToListAsync(ct);
        var registrations = await _context.CourseEnrollments.AsNoTracking()
            .Where(x => x.StudentId == studentId && offeringIds.Contains(x.CourseOfferingId) && x.Status == "Registered")
            .Include(x => x.CourseOffering).ThenInclude(x => x.Course).ToListAsync(ct);

        var maxCredits = await _context.LevelSemesterConfigs.AsNoTracking()
            .Where(x => x.LevelId == programmeEnrollment.LevelId && x.IsActive)
            .Select(x => (int?)x.MaxCreditLoad).MaxAsync(ct) ?? 24;

        var registeredDtos = registrations.Select(x => 
        {
            var credits = curriculumCreditMap.TryGetValue(x.CourseOffering.CourseId, out var cVal) ? cVal : (x.CourseOffering.Course?.CreditUnits ?? 0);
            return MapRegistration(x, x.CourseOffering, credits);
        }).ToList();

        var optionDtos = new List<RegistrationOfferingDto>();
        foreach (var offering in offerings)
        {
            var isRegistered = registrations.Any(x => x.CourseOfferingId == offering.Id);
            var blockers = isRegistered
                ? new List<RegistrationBlockerDto> { new("Registration.AlreadyRegistered", "Already registered.") }
                : await GetBlockersAsync(studentId, offering, ct);

            var credits = curriculumCreditMap.TryGetValue(offering.CourseId, out var cVal) ? cVal : (offering.Course?.CreditUnits ?? 0);

            optionDtos.Add(new RegistrationOfferingDto(
                offering.Id, offering.Course?.Code ?? string.Empty, offering.Course?.Title ?? string.Empty, credits,
                (int)offering.Semester,
                offering.Lecturers.FirstOrDefault(l => l.Role == Data.Enums.CourseLecturerRole.Main)?.Lecturer?.DisplayName ?? "To be announced",
                slots.Where(x => x.CourseOfferingId == offering.Id)
                    .Select(x => $"{x.DayOfWeek} {x.StartTime:HH\\:mm}–{x.EndTime:HH\\:mm}").ToList(),
                isRegistered, blockers.Count == 0, blockers,
                offering.Programs.Any(p => lowerLevelIds.Contains(p.LevelId))));
        }

        // Resolve active semester and dynamic min expected credit units requirement
        var activeSemester = session.ActiveSemester;

        int minCredits = 0;
        var config = await _context.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new SystemRegistrationConfiguration { Strategy = "Single", EnforceMinCredits = true };

        if (config.EnforceMinCredits && programmeEnrollment.CurriculumId != Guid.Empty)
        {
            minCredits = await _context.CurriculumCourses.AsNoTracking()
                .Where(cc => cc.CurriculumId == programmeEnrollment.CurriculumId &&
                             cc.LevelId == programmeEnrollment.LevelId &&
                             cc.Semester == activeSemester)
                .SumAsync(cc => (int?)cc.CreditUnits, ct) ?? 0;
        }

        var verification = await GetRegistrationVerificationAsync(studentId, session.Id, ct);
        return new RegistrationSummaryDto(studentId, studentName, session.Id, session.Name,
            registeredDtos.Sum(x => x.CreditUnits), maxCredits, registeredDtos, optionDtos, programName, levelName,
            config.Strategy, minCredits, verification is not null, verification?.VerifiedAtUtc);
    }

    private async Task<List<RegistrationBlockerDto>> GetBlockersAsync(Guid studentId, CourseOffering offering, CancellationToken ct)
    {
        var blockers = new List<RegistrationBlockerDto>();
        var programmeEnrollment = await ResolveProgrammeEnrollmentAsync(studentId, offering.AcademicSessionId, ct);

        var lowerLevelIds = new List<Guid>();
        var offeringLevelIds = new List<Guid>();

        // Check that the student's program is attached to this offering
        var programAttached = programmeEnrollment != null && await _context.CourseOfferingPrograms.AnyAsync(
            p => p.CourseOfferingId == offering.Id && p.ProgramId == programmeEnrollment.ProgramId, ct);
        if (programmeEnrollment is null || !programAttached)
        {
            blockers.Add(new("Registration.IneligibleOffering", "This course is not offered for your programme."));
        }
        else
        {
            var levels = await _context.Levels.AsNoTracking()
                .Where(x => x.ProgramId == programmeEnrollment.ProgramId)
                .ToListAsync(ct);
            var currentLevel = levels.FirstOrDefault(x => x.Id == programmeEnrollment.LevelId);
            lowerLevelIds = currentLevel != null 
                ? levels.Where(x => x.Order < currentLevel.Order).Select(x => x.Id).ToList() 
                : new List<Guid>();

            var eligibleLevelIds = new HashSet<Guid>(lowerLevelIds) { programmeEnrollment.LevelId };
            // Check that one of the offering's attached levels is eligible for this student
            offeringLevelIds = await _context.CourseOfferingPrograms
                .Where(p => p.CourseOfferingId == offering.Id && p.ProgramId == programmeEnrollment.ProgramId)
                .Select(p => p.LevelId)
                .ToListAsync(ct);
            if (!offeringLevelIds.Any(lid => eligibleLevelIds.Contains(lid)))
            {
                blockers.Add(new("Registration.IneligibleOffering", "This course is not offered for your programme and level."));
            }
        }

        if (offering.AcademicSession == null || !offering.AcademicSession.IsActive)
            blockers.Add(new("Registration.InactiveSession", "Registration is limited to the active academic session."));

        if (await _context.CourseEnrollments.AnyAsync(x => x.StudentId == studentId &&
            x.CourseOfferingId == offering.Id && x.Status == "Registered", ct))
            blockers.Add(new("Registration.AlreadyRegistered", "You are already registered for this course."));

        // Fetch grouped totals into memory first to avoid SQL divide-by-zero (SQL Server does not
        // guarantee short-circuit evaluation of AND inside HAVING clauses).
        var gradeGroupTotals = await _context.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.Assessment.CourseOffering.CourseId == offering.CourseId &&
                _context.GradePublications.Any(p => p.CourseOfferingId == g.Assessment.CourseOfferingId && p.IsVisibleToStudents))
            .GroupBy(g => new { g.Assessment.CourseOffering.CourseId, g.Assessment.CourseOfferingId })
            .Select(g => new { TotalMax = g.Sum(x => x.Assessment.MaxMarks), TotalObtained = g.Sum(x => x.MarksObtained) })
            .ToListAsync(ct);
        var courseAlreadyPassed = gradeGroupTotals.Any(g => g.TotalMax > 0 && g.TotalObtained / g.TotalMax * 100m >= 40m);
        if (courseAlreadyPassed)
            blockers.Add(new("Registration.AlreadyPassed", "You have already passed this course."));

        // Resolve curriculum — by admission session, then program active curriculum
        var resolvedCurriculumId = programmeEnrollment != null ? await ResolveCurriculumIdAsync(studentId, programmeEnrollment, ct) : Guid.Empty;

        var curriculumCreditMap = new Dictionary<Guid, int>();
        var curriculumCourseSemesters = new Dictionary<Guid, LMS.Api.Data.Enums.Semester>();
        if (resolvedCurriculumId != Guid.Empty)
        {
            var ccList = await _context.CurriculumCourses.AsNoTracking()
                .Where(x => x.CurriculumId == resolvedCurriculumId)
                .ToListAsync(ct);
            var distinctCCs = ccList.GroupBy(x => x.CourseId).Select(g => g.First()).ToList();
            curriculumCreditMap = distinctCCs.ToDictionary(x => x.CourseId, x => x.CreditUnits);
            curriculumCourseSemesters = distinctCCs.ToDictionary(x => x.CourseId, x => x.Semester);

            if (!curriculumCreditMap.ContainsKey(offering.CourseId))
            {
                blockers.Add(new("Registration.NotInCurriculum", "This course is not in your assigned curriculum."));
            }
            else if (offering.AcademicSession != null && curriculumCourseSemesters.TryGetValue(offering.CourseId, out var mappedSem) && mappedSem != offering.AcademicSession.ActiveSemester)
            {
                blockers.Add(new("Registration.WrongSemester", $"This course is scheduled for {mappedSem} semester in your curriculum."));
            }
        }

        var maxCredits = programmeEnrollment != null 
            ? (await _context.LevelSemesterConfigs.AsNoTracking()
                .Where(x => x.LevelId == programmeEnrollment.LevelId && x.Semester == offering.Semester && x.IsActive)
                .Select(x => (int?)x.MaxCreditLoad).FirstOrDefaultAsync(ct) ?? 24)
            : 24;

        var enrolledOfferings = await _context.CourseEnrollments.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.Status == "Registered" &&
                x.CourseOffering.AcademicSessionId == offering.AcademicSessionId && x.CourseOffering.Semester == offering.Semester)
            .Select(x => new { x.CourseOffering.CourseId, CreditUnits = x.CourseOffering.Course != null ? x.CourseOffering.Course.CreditUnits : 0 })
            .ToListAsync(ct);

        var currentCredits = enrolledOfferings.Sum(x => curriculumCreditMap.TryGetValue(x.CourseId, out var c) ? c : x.CreditUnits);
        var offeringCredits = curriculumCreditMap.TryGetValue(offering.CourseId, out var cVal) ? cVal : (offering.Course?.CreditUnits ?? 0);

        if (currentCredits + offeringCredits > maxCredits)
            blockers.Add(new("Registration.CreditLimitExceeded", $"Adding this course would exceed the {maxCredits}-credit limit."));

        // If the offering is at the student's current level, verify all carryovers are registered
        var offeringLevel = offeringLevelIds.FirstOrDefault();
        if (offeringLevel == programmeEnrollment!.LevelId)
        {
            var carryoverOfferings = await _context.CourseOfferings.AsNoTracking()
                .Where(x => x.AcademicSessionId == offering.AcademicSessionId &&
                            x.Semester == offering.Semester &&
                            _context.CourseOfferingPrograms.Any(p =>
                                p.CourseOfferingId == x.Id &&
                                p.ProgramId == programmeEnrollment.ProgramId &&
                                lowerLevelIds.Contains(p.LevelId)))
                .Include(x => x.Course)
                .ToListAsync(ct);

            if (carryoverOfferings.Count > 0)
            {
                var allGrades = await _context.Grades.AsNoTracking()
                    .Where(g => g.StudentId == studentId &&
                        _context.GradePublications.Any(p => p.CourseOfferingId == g.Assessment.CourseOfferingId && p.IsVisibleToStudents))
                    .Select(g => new { g.Assessment.CourseOffering.CourseId, g.Assessment.CourseOfferingId, g.MarksObtained, g.Assessment.MaxMarks })
                    .ToListAsync(ct);

                var passedIds = allGrades.GroupBy(x => new { x.CourseId, x.CourseOfferingId })
                    .Where(g => g.Sum(x => x.MaxMarks) > 0 && g.Sum(x => x.MarksObtained) / g.Sum(x => x.MaxMarks) * 100m >= 40m)
                    .Select(g => g.Key.CourseId).ToHashSet();

                var unpassedCarryovers = carryoverOfferings
                    .Where(x => !passedIds.Contains(x.CourseId))
                    .ToList();

                if (unpassedCarryovers.Count > 0)
                {
                    var registeredCarryoverOfferingIds = await _context.CourseEnrollments.AsNoTracking()
                        .Where(x => x.StudentId == studentId &&
                                    x.Status == "Registered" &&
                                    unpassedCarryovers.Select(c => c.Id).Contains(x.CourseOfferingId))
                        .Select(x => x.CourseOfferingId)
                        .ToListAsync(ct);

                    var unregisteredCarryovers = unpassedCarryovers
                        .Where(x => !registeredCarryoverOfferingIds.Contains(x.Id))
                        .Select(x => x.Course.Code)
                        .Distinct()
                        .ToList();

                    if (unregisteredCarryovers.Count > 0)
                    {
                        var codes = string.Join(", ", unregisteredCarryovers);
                        blockers.Add(new("Registration.CarryoverOutstanding", $"You must register for carryover course(s) first: {codes}."));
                    }
                }
            }
        }

        var requestedSlots = await _context.LectureTimetableSlots.AsNoTracking()
            .Where(x => x.CourseOfferingId == offering.Id).ToListAsync(ct);
        var registeredSlots = await _context.LectureTimetableSlots.AsNoTracking()
            .Where(x => _context.CourseEnrollments.Any(e => e.StudentId == studentId && e.Status == "Registered" && e.CourseOfferingId == x.CourseOfferingId))
            .ToListAsync(ct);
        if (requestedSlots.Any(a => registeredSlots.Any(b => a.DayOfWeek == b.DayOfWeek && a.StartTime < b.EndTime && b.StartTime < a.EndTime)))
            blockers.Add(new("Registration.TimetableConflict", "This course clashes with your current timetable."));

        var prerequisiteIds = await _context.CoursePrerequisites.AsNoTracking()
            .Where(x => x.CourseId == offering.CourseId && x.Type == Data.Enums.PrerequisiteType.HardPrerequisite)
            .Select(x => x.PrerequisiteCourseId).ToListAsync(ct);
        if (prerequisiteIds.Count > 0)
        {
            var approvedOverride = await _context.PrerequisiteOverrides.AnyAsync(x => x.StudentId == studentId &&
                x.CourseOfferingId == offering.Id && x.Status == "Approved", ct);
            if (!approvedOverride)
            {
                var gradeRows = await _context.Grades.AsNoTracking()
                    .Where(g => g.StudentId == studentId && prerequisiteIds.Contains(g.Assessment.CourseOffering.CourseId) &&
                        _context.GradePublications.Any(p => p.CourseOfferingId == g.Assessment.CourseOfferingId && p.IsVisibleToStudents))
                    .Select(g => new { g.Assessment.CourseOffering.CourseId, g.Assessment.CourseOfferingId, g.MarksObtained, g.Assessment.MaxMarks })
                    .ToListAsync(ct);
                var passed = gradeRows.GroupBy(x => new { x.CourseId, x.CourseOfferingId })
                    .Where(g => g.Sum(x => x.MaxMarks) > 0 && g.Sum(x => x.MarksObtained) / g.Sum(x => x.MaxMarks) * 100m >= 40m)
                    .Select(g => g.Key.CourseId).ToHashSet();
                var missing = prerequisiteIds.Where(x => !passed.Contains(x)).ToList();
                if (missing.Count > 0)
                    blockers.Add(new("Registration.PrerequisitesNotMet", "One or more required prerequisite courses have not been passed."));
            }
        }
        return blockers;
    }

    private Task<CourseOffering?> LoadOfferingAsync(Guid id, CancellationToken ct) =>
        _context.CourseOfferings
            .Include(x => x.Course)
            .Include(x => x.AcademicSession)
            .Include(x => x.Programs).ThenInclude(p => p.Program)
            .Include(x => x.Programs).ThenInclude(p => p.Level)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    private static CourseRegistrationDto MapRegistration(CourseEnrollment enrollment, CourseOffering offering, int creditUnits) =>
        new(enrollment.Id, enrollment.StudentId, offering.Id, offering.Course?.Code ?? string.Empty, offering.Course?.Title ?? string.Empty,
            enrollment.RegisteredAtUtc, enrollment.DroppedAtUtc, enrollment.Status, creditUnits);

    private async Task<Guid> GetProgramIdFromOffering(Guid courseOfferingId, CancellationToken ct)
    {
        return await _context.CourseOfferingPrograms
            .Where(p => p.CourseOfferingId == courseOfferingId)
            .Select(p => (Guid?)p.ProgramId)
            .FirstOrDefaultAsync(ct) ?? Guid.Empty;
    }

    /// <summary>
    /// Resolves the correct curriculum for a student's program enrollment.
    /// Priority order:
    ///   1. Explicitly stored CurriculumId on the enrollment
    ///   2. Active curriculum whose AdmissionSessionId matches the student's admission session
    ///   3. Latest active curriculum for the program (fallback)
    /// </summary>
    private async Task<Guid> ResolveCurriculumIdAsync(Guid studentId, ProgramEnrollment enrollment, CancellationToken ct)
    {
        if (enrollment.CurriculumId != Guid.Empty)
            return enrollment.CurriculumId;

        // Look up the student's admission session
        var student = await _context.Set<Student>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.EntraObjectId != null
                ? _context.Users.Any(u => u.Id == studentId && u.EntraObjectId == s.EntraObjectId)
                : _context.Users.Any(u => u.Id == studentId && u.Email == s.OfficialEmail), ct)
            ?? await _context.Set<Student>().AsNoTracking()
                .Where(s => _context.Users.Any(u => u.Id == studentId && (u.Email == s.OfficialEmail || u.Email == s.PersonalEmail)))
                .FirstOrDefaultAsync(ct);

        if (student != null)
        {
            // Try to match curriculum by the student's admission session
            var admissionSessionCurriculum = await _context.Curricula.AsNoTracking()
                .Where(c => c.ProgramId == enrollment.ProgramId &&
                            c.IsActive &&
                            c.AdmissionSessionId == student.AcademicSessionId)
                .OrderByDescending(c => c.CreatedUtc)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (admissionSessionCurriculum != Guid.Empty)
                return admissionSessionCurriculum;
        }

        // Final fallback: latest active curriculum for the program
        return await _context.Curricula.AsNoTracking()
            .Where(c => c.ProgramId == enrollment.ProgramId && c.IsActive)
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<int> GetCurriculumCreditsAsync(Guid studentId, Guid courseId, CancellationToken ct)
    {
        var session = await _context.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct);
        if (session != null)
        {
            var enrollment = await _context.Enrollments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == studentId && x.AcademicSessionId == session.Id, ct);
            if (enrollment != null && enrollment.CurriculumId != Guid.Empty)
            {
                var curriculumCourse = await _context.CurriculumCourses.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.CurriculumId == enrollment.CurriculumId && x.CourseId == courseId, ct);
                if (curriculumCourse != null)
                {
                    return curriculumCourse.CreditUnits;
                }
            }
        }
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == courseId, ct);
        return course?.CreditUnits ?? 0;
    }

    private async Task<RegistrationVerification?> GetRegistrationVerificationAsync(Guid studentId, Guid academicSessionId, CancellationToken ct) =>
        await _context.RegistrationVerifications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == studentId && x.AcademicSessionId == academicSessionId && x.Status == "Verified", ct);

    private async Task<bool> IsRegistrationLockedAsync(Guid studentId, Guid academicSessionId, CancellationToken ct) =>
        await _context.RegistrationVerifications.AsNoTracking()
            .AnyAsync(x => x.StudentId == studentId && x.AcademicSessionId == academicSessionId && x.Status == "Verified", ct);

    public async Task<ErrorOr<RegistrationSummaryDto>> RegisterCoursesBulk(Guid studentId, List<Guid> courseOfferingIds, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID must be provided.");
        }

        var config = await _context.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new SystemRegistrationConfiguration { Strategy = "Single", EnforceMinCredits = true };

        if (config.Strategy != "Bulk")
        {
            return Error.Validation("Registration.BulkDisabled", "Bulk registration is not enabled in system configuration.");
        }

        // Get active session
        var session = await _context.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct);
        if (session is null)
            return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");

        if (await IsRegistrationLockedAsync(studentId, session.Id, ct))
            return Error.Conflict("Registration.VerifiedLocked", "Your registration has been verified by your course adviser and can no longer be changed by you.");

        // Get student program enrollment
        var programmeEnrollment = await ResolveProgrammeEnrollmentAsync(studentId, session.Id, ct);
        if (programmeEnrollment is null)
            return Error.NotFound("Enrollment.NotFound", "No active program enrollment found for this session.");

        var levels = await _context.Levels.AsNoTracking()
            .Where(x => x.ProgramId == programmeEnrollment.ProgramId)
            .ToListAsync(ct);
        var currentLevel = levels.FirstOrDefault(x => x.Id == programmeEnrollment.LevelId);
        var lowerLevelIds = currentLevel != null 
            ? levels.Where(x => x.Order < currentLevel.Order).Select(x => x.Id).ToList() 
            : new List<Guid>();

        // Load curriculum credits map — resolved by admission session, then program active curriculum
        var resolvedCurriculumId = await ResolveCurriculumIdAsync(studentId, programmeEnrollment, ct);

        var curriculumCreditMap = new Dictionary<Guid, int>();
        var curriculumCourseSemesters = new Dictionary<Guid, LMS.Api.Data.Enums.Semester>();
        if (resolvedCurriculumId != Guid.Empty)
        {
            var ccList = await _context.CurriculumCourses.AsNoTracking()
                .Where(x => x.CurriculumId == resolvedCurriculumId)
                .ToListAsync(ct);
            var distinctCCs = ccList.GroupBy(x => x.CourseId).Select(g => g.First()).ToList();
            curriculumCreditMap = distinctCCs.ToDictionary(x => x.CourseId, x => x.CreditUnits);
            curriculumCourseSemesters = distinctCCs.ToDictionary(x => x.CourseId, x => x.Semester);
        }

        // Load all requested offerings
        var requestedOfferings = await _context.CourseOfferings
            .Where(o => courseOfferingIds.Contains(o.Id) && o.AcademicSessionId == session.Id && o.Semester == session.ActiveSemester)
            .Include(o => o.Course)
            .Include(o => o.AcademicSession)
            .ToListAsync(ct);

        // Note: Students are permitted to select courses outside their strict curriculum mapping as electives/global courses

        // Identify outstanding carryovers
        var allGradeRows = await _context.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId &&
                _context.GradePublications.Any(p => p.CourseOfferingId == g.Assessment.CourseOfferingId && p.IsVisibleToStudents))
            .Select(g => new { g.Assessment.CourseOffering.CourseId, g.Assessment.CourseOfferingId, g.MarksObtained, g.Assessment.MaxMarks })
            .ToListAsync(ct);

        var passedCourseIds = allGradeRows.GroupBy(x => new { x.CourseId, x.CourseOfferingId })
            .Where(g => g.Sum(x => x.MaxMarks) > 0 && g.Sum(x => x.MarksObtained) / g.Sum(x => x.MaxMarks) * 100m >= 40m)
            .Select(g => g.Key.CourseId).ToHashSet();

        // Carryover offerings for the semesters present in the student's program levels below current
        var carryoverOfferings = await _context.CourseOfferings.AsNoTracking()
            .Where(x => x.AcademicSessionId == session.Id &&
                        x.Semester == session.ActiveSemester &&
                        _context.CourseOfferingPrograms.Any(p =>
                            p.CourseOfferingId == x.Id &&
                            p.ProgramId == programmeEnrollment.ProgramId &&
                            lowerLevelIds.Contains(p.LevelId)) &&
                        !passedCourseIds.Contains(x.CourseId))
            .Include(x => x.Course)
            .ToListAsync(ct);

        if (programmeEnrollment.CurriculumId != Guid.Empty)
        {
            carryoverOfferings = carryoverOfferings.Where(x => 
                curriculumCreditMap.ContainsKey(x.CourseId) && 
                curriculumCourseSemesters.TryGetValue(x.CourseId, out var sem) && 
                sem == session.ActiveSemester
            ).ToList();
        }

        // Ensure all carryovers are selected
        var unregisteredCarryovers = carryoverOfferings
            .Where(c => !courseOfferingIds.Contains(c.Id))
            .Select(c => c.Course.Code)
            .Distinct()
            .ToList();

        if (unregisteredCarryovers.Count > 0)
        {
            var codes = string.Join(", ", unregisteredCarryovers);
            return Error.Validation("Registration.CarryoverOutstanding", $"You must register for all outstanding carryover course(s): {codes}.");
        }

        // Validate credit load and requirements per semester group
        var offeringsBySemester = requestedOfferings.GroupBy(o => o.Semester);
        foreach (var semGroup in offeringsBySemester)
        {
            var sem = semGroup.Key;
            var semOfferings = semGroup.ToList();
            var totalSemCredits = semOfferings.Sum(o => curriculumCreditMap.TryGetValue(o.CourseId, out var c) ? c : o.Course.CreditUnits);

            // Max credit config check
            var maxCredits = await _context.LevelSemesterConfigs.AsNoTracking()
                .Where(x => x.LevelId == programmeEnrollment.LevelId && x.Semester == sem && x.IsActive)
                .Select(x => (int?)x.MaxCreditLoad).FirstOrDefaultAsync(ct) ?? 24;

            if (totalSemCredits > maxCredits)
            {
                return Error.Validation("Registration.CreditLimitExceeded", $"Total credits ({totalSemCredits}) for {sem} semester exceed the limit of {maxCredits}.");
            }

            // Min expected credits check
            if (config.EnforceMinCredits)
            {
                var expectedCredits = await _context.CurriculumCourses.AsNoTracking()
                    .Where(cc => cc.CurriculumId == programmeEnrollment.CurriculumId &&
                                 cc.LevelId == programmeEnrollment.LevelId &&
                                 cc.Semester == sem)
                    .SumAsync(cc => (int?)cc.CreditUnits, ct) ?? 0;

                if (totalSemCredits < expectedCredits)
                {
                    return Error.Validation("Registration.MinCreditsNotMet", $"You must register for at least {expectedCredits} credits in {sem} semester (currently selected: {totalSemCredits} credits).");
                }
            }
        }

        // Timetable clash validation
        var slots = await _context.LectureTimetableSlots.AsNoTracking()
            .Where(s => courseOfferingIds.Contains(s.CourseOfferingId))
            .ToListAsync(ct);

        for (int i = 0; i < slots.Count; i++)
        {
            for (int j = i + 1; j < slots.Count; j++)
            {
                var s1 = slots[i];
                var s2 = slots[j];
                if (s1.DayOfWeek == s2.DayOfWeek && s1.StartTime < s2.EndTime && s2.StartTime < s1.EndTime)
                {
                    var o1 = requestedOfferings.First(o => o.Id == s1.CourseOfferingId);
                    var o2 = requestedOfferings.First(o => o.Id == s2.CourseOfferingId);
                    return Error.Validation("Registration.TimetableConflict", $"Timetable clash detected between {o1.Course.Code} and {o2.Course.Code} on {s1.DayOfWeek}.");
                }
            }
        }

        // Prerequisites validation
        foreach (var offering in requestedOfferings)
        {
            var prerequisiteIds = await _context.CoursePrerequisites.AsNoTracking()
                .Where(x => x.CourseId == offering.CourseId && x.Type == Data.Enums.PrerequisiteType.HardPrerequisite)
                .Select(x => x.PrerequisiteCourseId).ToListAsync(ct);

            if (prerequisiteIds.Count > 0)
            {
                var approvedOverride = await _context.PrerequisiteOverrides.AnyAsync(x => x.StudentId == studentId &&
                    x.CourseOfferingId == offering.Id && x.Status == "Approved", ct);

                if (!approvedOverride)
                {
                    var gradeRows = await _context.Grades.AsNoTracking()
                        .Where(g => g.StudentId == studentId && prerequisiteIds.Contains(g.Assessment.CourseOffering.CourseId) &&
                            _context.GradePublications.Any(p => p.CourseOfferingId == g.Assessment.CourseOfferingId && p.IsVisibleToStudents))
                        .Select(g => new { g.Assessment.CourseOffering.CourseId, g.Assessment.CourseOfferingId, g.MarksObtained, g.Assessment.MaxMarks })
                        .ToListAsync(ct);

                    var passed = gradeRows.GroupBy(x => new { x.CourseId, x.CourseOfferingId })
                        .Where(g => g.Sum(x => x.MaxMarks) > 0 && g.Sum(x => x.MarksObtained) / g.Sum(x => x.MaxMarks) * 100m >= 40m)
                        .Select(g => g.Key.CourseId).ToHashSet();

                    var missing = prerequisiteIds.Where(x => !passed.Contains(x)).ToList();
                    if (missing.Count > 0)
                    {
                        return Error.Validation("Registration.PrerequisitesNotMet", $"Prerequisites not met for {offering.Course?.Code ?? "this course"}.");
                    }
                }
            }
        }

        var dbStrategy = _context.Database.CreateExecutionStrategy();
        return await dbStrategy.ExecuteAsync<ErrorOr<RegistrationSummaryDto>>(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            // Get existing enrollments for the current session
            var existingEnrollments = await _context.CourseEnrollments
                .Where(x => x.StudentId == studentId && x.CourseOffering.AcademicSessionId == session.Id)
                .ToListAsync(ct);

            // Set dropped for any not in new selection
            foreach (var enrollment in existingEnrollments)
            {
                if (!courseOfferingIds.Contains(enrollment.CourseOfferingId))
                {
                    if (enrollment.Status == "Registered")
                    {
                        enrollment.Status = "Dropped";
                        enrollment.DroppedAtUtc = DateTime.UtcNow;
                        enrollment.UpdatedById = studentId;
                    }
                }
            }

            // Register/reactivate new selections
            foreach (var offeringId in courseOfferingIds)
            {
                var enrollment = existingEnrollments.FirstOrDefault(e => e.CourseOfferingId == offeringId);
                if (enrollment is null)
                {
                    enrollment = new CourseEnrollment
                    {
                        StudentId = studentId,
                        CourseOfferingId = offeringId,
                        Status = "Registered",
                        RegisteredAtUtc = DateTime.UtcNow,
                        CreatedById = studentId
                    };
                    _context.CourseEnrollments.Add(enrollment);
                }
                else if (enrollment.Status != "Registered")
                {
                    enrollment.Status = "Registered";
                    enrollment.RegisteredAtUtc = DateTime.UtcNow;
                    enrollment.DroppedAtUtc = null;
                    enrollment.UpdatedById = studentId;
                }
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await LogActionAsync("RegisterCoursesBulk", "CourseEnrollment", studentId.ToString(),
                $"Student {studentId} submitted bulk registration for {courseOfferingIds.Count} courses.", ct);

            return await GetRegistrationSummaryAsync(studentId, null, ct);
        });
    }

    public async Task<ErrorOr<List<RegistrationOfferingDto>>> GetGlobalCourseOfferingsAsync(Guid studentId, string? search = null, CancellationToken ct = default)
    {
        var session = await _context.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct);
        if (session is null)
            return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");

        var programmeEnrollment = await ResolveProgrammeEnrollmentAsync(studentId, session.Id, ct);

        var query = _context.CourseOfferings.AsNoTracking()
            .Where(x => x.AcademicSessionId == session.Id && x.Semester == session.ActiveSemester)
            .Include(x => x.Course)
            .Include(x => x.AcademicSession)
            .Include(x => x.Programs).ThenInclude(p => p.Level)
            .Include(x => x.Lecturers).ThenInclude(l => l.Lecturer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                (x.Course != null && (x.Course.Code.ToLower().Contains(s) || x.Course.Title.ToLower().Contains(s))) ||
                x.Lecturers.Any(l => l.Lecturer != null && (l.Lecturer.DisplayName.ToLower().Contains(s) || l.Lecturer.Email.ToLower().Contains(s))));
        }

        var offerings = await query.OrderBy(x => x.Course.Code).ToListAsync(ct);
        var offeringIds = offerings.Select(x => x.Id).ToList();

        var slots = await _context.LectureTimetableSlots.AsNoTracking()
            .Where(x => offeringIds.Contains(x.CourseOfferingId)).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).ToListAsync(ct);

        var registrations = await _context.CourseEnrollments.AsNoTracking()
            .Where(x => x.StudentId == studentId && offeringIds.Contains(x.CourseOfferingId) && x.Status == "Registered")
            .ToListAsync(ct);

        var resolvedCurriculumId = programmeEnrollment != null 
            ? await ResolveCurriculumIdAsync(studentId, programmeEnrollment, ct) 
            : Guid.Empty;

        var curriculumCreditMap = new Dictionary<Guid, int>();
        if (resolvedCurriculumId != Guid.Empty)
        {
            var ccList = await _context.CurriculumCourses.AsNoTracking()
                .Where(x => x.CurriculumId == resolvedCurriculumId)
                .ToListAsync(ct);
            curriculumCreditMap = ccList.GroupBy(x => x.CourseId).ToDictionary(g => g.Key, g => g.First().CreditUnits);
        }

        var resultDtos = new List<RegistrationOfferingDto>();
        foreach (var offering in offerings)
        {
            var isRegistered = registrations.Any(x => x.CourseOfferingId == offering.Id);
            var blockers = isRegistered
                ? new List<RegistrationBlockerDto> { new("Registration.AlreadyRegistered", "Already registered.") }
                : await GetBlockersAsync(studentId, offering, ct);

            var hardBlockers = blockers.Where(b => b.Code != "Registration.IneligibleOffering" &&
                                                  b.Code != "Registration.NotInCurriculum" &&
                                                  b.Code != "Registration.WrongSemester").ToList();

            var isExternal = blockers.Any(b => b.Code == "Registration.NotInCurriculum" || b.Code == "Registration.IneligibleOffering");
            var credits = curriculumCreditMap.TryGetValue(offering.CourseId, out var cVal) ? cVal : (offering.Course?.CreditUnits ?? 0);

            resultDtos.Add(new RegistrationOfferingDto(
                offering.Id,
                offering.Course?.Code ?? string.Empty,
                offering.Course?.Title ?? string.Empty,
                credits,
                (int)offering.Semester,
                offering.Lecturers.FirstOrDefault(l => l.Role == Data.Enums.CourseLecturerRole.Main)?.Lecturer?.DisplayName ?? "To be announced",
                slots.Where(x => x.CourseOfferingId == offering.Id).Select(x => $"{x.DayOfWeek} {x.StartTime:HH\\:mm}–{x.EndTime:HH\\:mm}").ToList(),
                isRegistered,
                hardBlockers.Count == 0,
                blockers,
                false,
                isExternal));
        }

        return resultDtos;
    }

    private async Task<ProgramEnrollment?> ResolveProgrammeEnrollmentAsync(Guid studentId, Guid academicSessionId, CancellationToken ct)
    {
        var programmeEnrollment = await _context.Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == studentId && x.AcademicSessionId == academicSessionId, ct);

        if (programmeEnrollment != null)
        {
            return programmeEnrollment;
        }

        // Try resolving from previous session enrollments
        var prevEnrollment = await _context.Enrollments.AsNoTracking()
            .Where(x => x.UserId == studentId)
            .OrderByDescending(x => x.EnrolledAtUtc)
            .FirstOrDefaultAsync(ct);

        // Try resolving from Student entity
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
                // Ignore concurrent save conflicts if created simultaneously by another request
            }

            return newEnrollment;
        }

        return null;
    }
}
