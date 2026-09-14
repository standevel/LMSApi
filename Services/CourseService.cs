using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class CourseService(
    ICourseRepository courseRepository,
    IUserRepository userRepository,
    IAuditService auditService,
    LmsDbContext dbContext,
    IFileStorageService fileStorageService,
    INotificationService notificationService,
    IEmailService emailService,
    IGradeCalculationEngine gradeCalculationEngine,
    Microsoft.Extensions.Logging.ILogger<CourseService> logger) : BaseService(auditService), ICourseService
{
    public static int? InferLevelOrderFromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(code, @"\d{3}");
        if (match.Success)
        {
            return match.Value[0] - '0';
        }
        return null;
    }

    public static Semester? InferSemesterFromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(code, @"\d{3}");
        if (match.Success)
        {
            int lastDigit = match.Value[^1] - '0';
            return (lastDigit % 2 == 1) ? Semester.First : Semester.Second;
        }
        return null;
    }

    // ─── Query helpers ────────────────────────────────────────────────────────

    private IQueryable<CourseOffering> OfferingsWithNavigations() =>
        dbContext.CourseOfferings
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .Include(co => co.Programs).ThenInclude(p => p.Program)
            .Include(co => co.Programs).ThenInclude(p => p.Level)
            .Include(co => co.Lecturers).ThenInclude(l => l.Lecturer);

    // ─── Course CRUD ──────────────────────────────────────────────────────────

    public async Task<ErrorOr<CourseDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course is null) return DomainErrors.Course.NotFound;

        var ccList = await dbContext.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Curriculum).ThenInclude(c => c.Program)
            .Include(cc => cc.Level)
            .Where(cc => cc.CourseId == id)
            .ToListAsync(ct);

        if (course.Offerings.Count == 0)
        {
            var activeSessions = await dbContext.AcademicSessions
                .Where(s => s.IsActive || s.IsAdmissionActive)
                .ToListAsync(ct);

            if (activeSessions.Count == 0)
            {
                activeSessions = await dbContext.AcademicSessions
                    .OrderByDescending(s => s.StartDate)
                    .Take(1)
                    .ToListAsync(ct);
            }

            if (activeSessions.Count > 0)
            {
                foreach (var session in activeSessions)
                {
                    var semester = course.Semester ?? Semester.First;
                    var offering = new CourseOffering
                    {
                        CourseId = course.Id,
                        AcademicSessionId = session.Id,
                        Semester = semester
                    };

                    if (ccList.Count > 0)
                    {
                        foreach (var cc in ccList)
                        {
                            if (cc.Curriculum?.ProgramId != null && cc.Curriculum.ProgramId != Guid.Empty)
                            {
                                offering.Programs.Add(new CourseOfferingProgram
                                {
                                    CourseOfferingId = offering.Id,
                                    ProgramId = cc.Curriculum.ProgramId,
                                    LevelId = cc.LevelId
                                });
                            }
                        }
                    }
                    else if (course.ProgramId != Guid.Empty)
                    {
                        offering.Programs.Add(new CourseOfferingProgram
                        {
                            CourseOfferingId = offering.Id,
                            ProgramId = course.ProgramId,
                            LevelId = course.LevelId ?? Guid.Empty
                        });
                    }

                    dbContext.CourseOfferings.Add(offering);
                    course.Offerings.Add(offering);
                }
                await dbContext.SaveChangesAsync(ct);
            }
        }

        return course.ToDto(ccList);
    }

    public async Task<ErrorOr<List<CourseDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var courses = await courseRepository.GetAllAsync(ct);
        var ccLookup = (await dbContext.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Curriculum).ThenInclude(c => c.Program)
            .Include(cc => cc.Level)
            .ToListAsync(ct))
            .ToLookup(cc => cc.CourseId);

        return courses.Select(c => c.ToDto(ccLookup[c.Id])).ToList();
    }

    public async Task<ErrorOr<CourseDto>> CreateAsync(CreateCourseRequest request, CancellationToken ct = default)
    {
        // Resolve the owning ProgramId: the client may send Guid.Empty when the program is
        // captured per-offering rather than at the top level.  Fall back to the first offering.
        var resolvedProgramId = request.ProgramId != Guid.Empty
            ? request.ProgramId
            : request.Offerings.FirstOrDefault(o => o.ProgramId.HasValue && o.ProgramId != Guid.Empty)?.ProgramId
              ?? Guid.Empty;

        if (resolvedProgramId == Guid.Empty)
            return Error.Validation("Course.ProgramRequired", "A program must be selected for the course.");

        var sanitizedCode = request.Code?.Replace("-", " ") ?? string.Empty;
        var inferredLevelOrder = InferLevelOrderFromCode(sanitizedCode);
        var inferredSemester = InferSemesterFromCode(sanitizedCode);

        // Check for duplicate course code within the same program
        var existingCourse = await dbContext.Courses
            .FirstOrDefaultAsync(c => c.ProgramId == resolvedProgramId && c.Code == sanitizedCode, ct);
        if (existingCourse != null)
        {
            return Error.Conflict("Course.DuplicateCode", $"Course code '{sanitizedCode}' already exists for the selected program.");
        }

        var resolvedLevelId = request.LevelId;
        if ((!resolvedLevelId.HasValue || resolvedLevelId.Value == Guid.Empty) && inferredLevelOrder.HasValue && resolvedProgramId != Guid.Empty)
        {
            resolvedLevelId = await dbContext.Levels
                .Where(l => l.ProgramId == resolvedProgramId && l.Order == inferredLevelOrder.Value)
                .Select(l => (Guid?)l.Id)
                .FirstOrDefaultAsync(ct);
        }

        var resolvedSemester = request.Semester ?? inferredSemester ?? Semester.First;

        var course = new Course
        {
            ProgramId   = resolvedProgramId,
            Code        = sanitizedCode,
            Title       = request.Title,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? $"A comprehensive {request.CreditUnits}-unit course on {request.Title} ({sanitizedCode})."
                : request.Description,
            CreditUnits = request.CreditUnits,
            LevelId     = resolvedLevelId,
            Semester    = resolvedSemester,
            IsActive    = true,
            // Offerings with session, semester, and optional program+level and lecturer
            Offerings = request.Offerings
                .GroupBy(o => new { o.AcademicSessionId, Semester = resolvedSemester })
                .Select(g => {
                    var progs = g.Where(r => r.ProgramId.HasValue && r.LevelId.HasValue && r.LevelId.Value != Guid.Empty)
                                .Select(r => new { r.ProgramId, r.LevelId })
                                .Distinct()
                                .Select(rp => new CourseOfferingProgram
                                {
                                    ProgramId = rp.ProgramId!.Value,
                                    LevelId = rp.LevelId!.Value
                                }).ToList();

                    if (progs.Count == 0 && resolvedProgramId != Guid.Empty && resolvedLevelId.HasValue)
                    {
                        progs.Add(new CourseOfferingProgram
                        {
                            ProgramId = resolvedProgramId,
                            LevelId = resolvedLevelId.Value
                        });
                    }

                    var lecs = new List<CourseOfferingLecturer>();
                    var mainLecId = g.FirstOrDefault(r => r.LecturerId.HasValue && r.LecturerId.Value != Guid.Empty)?.LecturerId;
                    if (mainLecId.HasValue)
                    {
                        lecs.Add(new CourseOfferingLecturer
                        {
                            LecturerId = mainLecId.Value,
                            Role = CourseLecturerRole.Main
                        });
                    }

                    return new CourseOffering
                    {
                        AcademicSessionId = g.Key.AcademicSessionId,
                        Semester          = g.Key.Semester,
                        Programs          = progs,
                        Lecturers         = lecs
                    };
                }).ToList()
        };

        await courseRepository.AddAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Course", course.Id.ToString(),
            $"Created course: {course.Code} - {course.Title}", ct);

        var createdCourse = await courseRepository.GetByIdAsync(course.Id, ct);
        return createdCourse!.ToDto();
    }

    public async Task<ErrorOr<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        // Validate FK references up front so we return a clear error instead of a 500
        // when a program/level GUID from the client doesn't exist in the database.
        var requestedPairs = request.Offerings
            .Where(o => o.ProgramId.HasValue && o.LevelId.HasValue)
            .Select(o => new { ProgramId = o.ProgramId!.Value, LevelId = o.LevelId!.Value })
            .Distinct()
            .ToList();

        if (requestedPairs.Count > 0)
        {
            var programIds = requestedPairs.Select(p => p.ProgramId).ToHashSet();
            var levelIds = requestedPairs.Select(p => p.LevelId).ToHashSet();

            var existingProgramIds = (await dbContext.Programs
                .Where(p => programIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct)).ToHashSet();

            var existingLevelIds = (await dbContext.Levels
                .Where(l => levelIds.Contains(l.Id))
                .Select(l => l.Id)
                .ToListAsync(ct)).ToHashSet();

            var missingPrograms = programIds.Except(existingProgramIds).ToList();
            var missingLevels = levelIds.Except(existingLevelIds).ToList();

            if (missingPrograms.Count > 0 || missingLevels.Count > 0)
            {
                var messages = new List<string>();
                if (missingPrograms.Count > 0)
                    messages.Add($"Program(s) not found: {string.Join(", ", missingPrograms)}");
                if (missingLevels.Count > 0)
                    messages.Add($"Level(s) not found: {string.Join(", ", missingLevels)}");
                return Error.Validation("Course.OfferingReferenceNotFound",
                    string.Join("; ", messages));
            }
        }

        // Validate the top-level owning program/level as well (used by the unique index + fallback).
        if (request.ProgramId.HasValue && request.ProgramId.Value != Guid.Empty)
        {
            var programExists = await dbContext.Programs
                .AnyAsync(p => p.Id == request.ProgramId.Value, ct);
            if (!programExists)
            {
                return Error.Validation("Course.ProgramNotFound",
                    $"Program not found: {request.ProgramId.Value}");
            }
        }

        if (request.ProgramId.HasValue && request.ProgramId.Value != Guid.Empty)
        {
            course.ProgramId = request.ProgramId.Value;
        }
        else
        {
            var fallbackProgId = request.Offerings.FirstOrDefault(o => o.ProgramId.HasValue && o.ProgramId != Guid.Empty)?.ProgramId;
            if (fallbackProgId.HasValue && fallbackProgId.Value != Guid.Empty)
            {
                course.ProgramId = fallbackProgId.Value;
            }
        }

        course.Code        = request.Code?.Replace("-", " ") ?? string.Empty;
        course.Title       = request.Title;
        course.Description = string.IsNullOrWhiteSpace(request.Description)
            ? $"A comprehensive {request.CreditUnits}-unit course on {request.Title} ({course.Code})."
            : request.Description;
        course.CreditUnits = request.CreditUnits;

        var inferredLevelOrder = InferLevelOrderFromCode(course.Code);
        var inferredSemester = InferSemesterFromCode(course.Code);
        
        var resolvedLevelId = request.LevelId.HasValue && request.LevelId.Value != Guid.Empty
            ? request.LevelId
            : request.Offerings.FirstOrDefault(o => o.LevelId.HasValue && o.LevelId != Guid.Empty)?.LevelId;

        if ((!resolvedLevelId.HasValue || resolvedLevelId.Value == Guid.Empty) && inferredLevelOrder.HasValue && course.ProgramId != Guid.Empty)
        {
            resolvedLevelId = await dbContext.Levels
                .Where(l => l.ProgramId == course.ProgramId && l.Order == inferredLevelOrder.Value)
                .Select(l => (Guid?)l.Id)
                .FirstOrDefaultAsync(ct);
        }

        var targetSemester = inferredSemester ?? request.Semester ?? course.Semester ?? Semester.First;
        course.LevelId     = resolvedLevelId;
        course.Semester    = targetSemester;

        var uniqueOfferingRequests = request.Offerings
            .GroupBy(r => new { r.AcademicSessionId, Semester = (int)targetSemester })
            .ToList();

        // 1. Add or Update offerings and sync programs and lecturers
        foreach (var offeringGroup in uniqueOfferingRequests)
        {
            var session = offeringGroup.Key.AcademicSessionId;
            var sem = targetSemester;

            var offering = course.Offerings.FirstOrDefault(o => o.AcademicSessionId == session && o.Semester == sem);
            
            if (offering == null)
            {
                // Repurpose an existing wrong-semester offering for this session if available
                var wrongSemOffering = course.Offerings.FirstOrDefault(o => o.AcademicSessionId == session && o.Semester != sem);
                if (wrongSemOffering != null)
                {
                    wrongSemOffering.Semester = sem;
                    offering = wrongSemOffering;
                }
                else
                {
                    offering = new CourseOffering
                    {
                        Id = Guid.NewGuid(),
                        CourseId = id,
                        AcademicSessionId = session,
                        Semester = sem,
                        Programs = new List<CourseOfferingProgram>(),
                        Lecturers = new List<CourseOfferingLecturer>()
                    };
                    course.Offerings.Add(offering);
                }
            }

            // Sync programs for this offering
            var requestedPrograms = offeringGroup
                .Where(r => r.ProgramId.HasValue && r.LevelId.HasValue)
                .Select(r => new { ProgramId = r.ProgramId!.Value, LevelId = r.LevelId!.Value })
                .Distinct()
                .ToList();

            if (requestedPrograms.Count == 0 && course.ProgramId != Guid.Empty && course.LevelId.HasValue)
            {
                requestedPrograms.Add(new { ProgramId = course.ProgramId, LevelId = course.LevelId.Value });
            }

            var existingPrograms = offering.Programs.ToList();
            if (offering.Id != Guid.Empty)
            {
                var dbPrograms = await dbContext.CourseOfferingPrograms
                    .Where(p => p.CourseOfferingId == offering.Id)
                    .ToListAsync(ct);
                foreach (var dbp in dbPrograms)
                {
                    if (!existingPrograms.Any(p => p.Id == dbp.Id))
                    {
                        existingPrograms.Add(dbp);
                        offering.Programs.Add(dbp);
                    }
                }
            }

            // Remove program-level links that are no longer requested
            var programsToRemove = existingPrograms
                .Where(ep => !requestedPrograms.Any(rp => rp.ProgramId == ep.ProgramId && rp.LevelId == ep.LevelId))
                .ToList();

            foreach (var toRemove in programsToRemove)
            {
                offering.Programs.Remove(toRemove);
                dbContext.CourseOfferingPrograms.Remove(toRemove);
                existingPrograms.Remove(toRemove);
            }

            // Add newly requested program-level links
            foreach (var rp in requestedPrograms)
            {
                if (!existingPrograms.Any(p => p.ProgramId == rp.ProgramId && p.LevelId == rp.LevelId))
                {
                    var newProg = new CourseOfferingProgram
                    {
                        Id = Guid.NewGuid(),
                        CourseOfferingId = offering.Id,
                        ProgramId = rp.ProgramId,
                        LevelId = rp.LevelId
                    };
                    offering.Programs.Add(newProg);
                    dbContext.CourseOfferingPrograms.Add(newProg);
                    existingPrograms.Add(newProg);
                }
            }

            // Sync main lecturer for this offering
            var existingLecturers = offering.Lecturers.ToList();
            if (offering.Id != Guid.Empty)
            {
                var dbLecturers = await dbContext.CourseOfferingLecturers
                    .Where(l => l.CourseOfferingId == offering.Id)
                    .ToListAsync(ct);
                foreach (var dbl in dbLecturers)
                {
                    if (!existingLecturers.Any(l => l.Id == dbl.Id))
                    {
                        existingLecturers.Add(dbl);
                    }
                }
            }

            var requestedLecturerItem = offeringGroup
                .FirstOrDefault(r => r.LecturerId.HasValue && r.LecturerId.Value != Guid.Empty);
            var requestedLecturerId = requestedLecturerItem?.LecturerId;

            if (requestedLecturerId.HasValue && requestedLecturerId.Value != Guid.Empty)
            {
                var targetId = requestedLecturerId.Value;

                // 1. Remove any other lecturer previously assigned as Main
                var otherMains = existingLecturers
                    .Where(l => l.LecturerId != targetId && l.Role == CourseLecturerRole.Main)
                    .ToList();
                foreach (var otherMain in otherMains)
                {
                    offering.Lecturers.Remove(otherMain);
                    dbContext.CourseOfferingLecturers.Remove(otherMain);
                    existingLecturers.Remove(otherMain);
                }

                // 2. Check if target lecturer is already assigned to this offering (e.g. as CoLecturer)
                var targetLecturerRows = existingLecturers.Where(l => l.LecturerId == targetId).ToList();
                if (targetLecturerRows.Count > 0)
                {
                    var primary = targetLecturerRows[0];
                    primary.Role = CourseLecturerRole.Main;
                    if (!offering.Lecturers.Contains(primary))
                    {
                        offering.Lecturers.Add(primary);
                    }

                    // Clean up any extraneous duplicate rows if they somehow existed
                    for (int i = 1; i < targetLecturerRows.Count; i++)
                    {
                        offering.Lecturers.Remove(targetLecturerRows[i]);
                        dbContext.CourseOfferingLecturers.Remove(targetLecturerRows[i]);
                        existingLecturers.Remove(targetLecturerRows[i]);
                    }
                }
                else
                {
                    var newLecturer = new CourseOfferingLecturer
                    {
                        Id = Guid.NewGuid(),
                        CourseOfferingId = offering.Id,
                        LecturerId = targetId,
                        Role = CourseLecturerRole.Main
                    };
                    offering.Lecturers.Add(newLecturer);
                    dbContext.CourseOfferingLecturers.Add(newLecturer);
                    existingLecturers.Add(newLecturer);
                }
            }
            else
            {
                // No lecturer requested (or cleared): remove any existing Main lecturer
                var existingMains = existingLecturers
                    .Where(l => l.Role == CourseLecturerRole.Main)
                    .ToList();
                foreach (var main in existingMains)
                {
                    offering.Lecturers.Remove(main);
                    dbContext.CourseOfferingLecturers.Remove(main);
                    existingLecturers.Remove(main);
                }
            }
        }

        // 2. Remove offerings (and their related links) that are not in the request.
        //    Use ExecuteDeleteAsync (direct SQL) to bypass the EF change tracker — this avoids
        //    DbUpdateConcurrencyException caused by auto-provisioned in-memory offerings whose
        //    rows may not actually exist in the DB yet.
        var offeringsToRemove = course.Offerings
            .Where(existing =>
                existing.Id != Guid.Empty &&  // skip un-persisted auto-provisioned offerings
                !uniqueOfferingRequests.Any(g =>
                    g.Key.AcademicSessionId == existing.AcademicSessionId &&
                    g.Key.Semester == (int)existing.Semester))
            .ToList();

        foreach (var toRemove in offeringsToRemove)
        {
            var validOffering = course.Offerings.FirstOrDefault(o => o.AcademicSessionId == toRemove.AcademicSessionId && o.Semester == targetSemester && o.Id != toRemove.Id);
            if (validOffering != null)
            {
                var wrongEnrollments = await dbContext.CourseEnrollments.Where(e => e.CourseOfferingId == toRemove.Id).ToListAsync(ct);
                var validStudentIds = (await dbContext.CourseEnrollments.Where(e => e.CourseOfferingId == validOffering.Id).Select(e => e.StudentId).ToListAsync(ct)).ToHashSet();
                foreach (var we in wrongEnrollments)
                {
                    if (validStudentIds.Contains(we.StudentId))
                    {
                        dbContext.CourseEnrollments.Remove(we);
                    }
                    else
                    {
                        we.CourseOfferingId = validOffering.Id;
                    }
                }

                var categories = await dbContext.AssessmentCategories.Where(c => c.CourseOfferingId == toRemove.Id).ToListAsync(ct);
                foreach (var cat in categories)
                {
                    cat.CourseOfferingId = validOffering.Id;
                }

                var assessments = await dbContext.Assessments.Where(a => a.CourseOfferingId == toRemove.Id).ToListAsync(ct);
                foreach (var a in assessments)
                {
                    a.CourseOfferingId = validOffering.Id;
                }

                await dbContext.SaveChangesAsync(ct);
            }
            else
            {
                var hasEnrollments = await dbContext.CourseEnrollments.AnyAsync(e => e.CourseOfferingId == toRemove.Id, ct);
                if (hasEnrollments)
                {
                    continue;
                }
            }

            // Step 1: Direct SQL deletes for all related child tables first to prevent FK constraint failures
            await dbContext.CourseOfferingLecturers
                .Where(l => l.CourseOfferingId == toRemove.Id)
                .ExecuteDeleteAsync(ct);

            await dbContext.CourseOfferingPrograms
                .Where(p => p.CourseOfferingId == toRemove.Id)
                .ExecuteDeleteAsync(ct);

            await dbContext.CourseOfferings
                .Where(o => o.Id == toRemove.Id)
                .ExecuteDeleteAsync(ct);

            // Step 2: Detach ALL related entities from the change tracker BEFORE touching the collection.
            // Without this, EF re-queues a DELETE for already-deleted rows during SaveChanges,
            // resulting in DbUpdateConcurrencyException (0 rows affected).
            foreach (var prog in toRemove.Programs?.ToList() ?? [])
            {
                dbContext.Entry(prog).State = EntityState.Detached;
            }
            foreach (var lec in toRemove.Lecturers?.ToList() ?? [])
            {
                dbContext.Entry(lec).State = EntityState.Detached;
            }
            dbContext.Entry(toRemove).State = EntityState.Detached;

            var orphanEntries = dbContext.ChangeTracker.Entries()
                .Where(e =>
                    (e.Entity is CourseOffering co && co.Id == toRemove.Id) ||
                    (e.Entity is CourseOfferingProgram cop && cop.CourseOfferingId == toRemove.Id) ||
                    (e.Entity is CourseOfferingLecturer col && col.CourseOfferingId == toRemove.Id))
                .ToList();

            foreach (var entry in orphanEntries)
            {
                entry.State = EntityState.Detached;
            }

            // Step 3: Remove from the in-memory collection (now safe — entity and children are detached).
            course.Offerings.Remove(toRemove);
        }

        try
        {
            await courseRepository.UpdateAsync(course, ct);
            await courseRepository.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException concEx)
        {
            logger.LogError(concEx, "DbUpdateConcurrencyException updating course {CourseId}: {Message}", id, concEx.Message);
            return Error.Conflict("Course.ConcurrencyConflict",
                "The course or its offerings were modified concurrently. Please refresh and try again.");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            logger.LogError(dbEx, "DbUpdateException updating course {CourseId}: {Message}", id, dbEx.InnerException?.Message ?? dbEx.Message);
            return Error.Failure("Course.UpdateFailed",
                $"Failed to save course changes: {dbEx.InnerException?.Message ?? dbEx.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating course {CourseId}: {Message}", id, ex.InnerException?.Message ?? ex.Message);
            return Error.Failure("Course.UpdateFailed",
                "An unexpected error occurred while updating the course.");
        }

        await LogActionAsync("Update", "Course", id.ToString(), $"Updated course: {course.Code}", ct);

        var ccList = await dbContext.CurriculumCourses
            .AsNoTracking()
            .Include(cc => cc.Curriculum).ThenInclude(c => c.Program)
            .Include(cc => cc.Level)
            .Where(cc => cc.CourseId == id)
            .ToListAsync(ct);

        var updatedCourse = await courseRepository.GetByIdAsync(id, ct);
        return updatedCourse!.ToDto(ccList);
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        // Check if there are any dependent records in dbContext directly to prevent DB exception
        var hasOfferings = await dbContext.CourseOfferings.AnyAsync(x => x.CourseId == id, ct);
        if (hasOfferings)
        {
            return Error.Conflict("Course.HasOfferings", "Cannot delete this course because it has active course offerings.");
        }

        var hasCurriculum = await dbContext.CurriculumCourses.AnyAsync(x => x.CourseId == id, ct);
        if (hasCurriculum)
        {
            return Error.Conflict("Course.HasCurriculum", "Cannot delete this course because it is part of a curriculum.");
        }

        var hasDegreeReq = await dbContext.DegreeRequirementCourses.AnyAsync(x => x.CourseId == id, ct);
        if (hasDegreeReq)
        {
            return Error.Conflict("Course.HasDegreeRequirements", "Cannot delete this course because it is linked to degree requirements.");
        }

        var hasAssignments = await dbContext.Assignments.AnyAsync(x => x.CourseOffering.CourseId == id, ct);
        if (hasAssignments)
        {
            return Error.Conflict("Course.HasAssignments", "Cannot delete this course because it has associated assignments.");
        }

        await courseRepository.DeleteAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Course", id.ToString(), $"Deleted course: {course.Code}", ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<CourseDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        course.IsActive = !course.IsActive;
        await courseRepository.UpdateAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("ToggleStatus", "Course", id.ToString(),
            $"Toggled status for course {course.Code} to {course.IsActive}", ct);

        return course.ToDto();
    }

    // ─── Lecturers list ───────────────────────────────────────────────────────

    public async Task<ErrorOr<List<SimpleUserDto>>> GetLecturersAsync(CancellationToken ct = default)
    {
        var lecturers = await userRepository.GetByRoleAsync("Lecturer", ct);
        return lecturers.Select(u => new SimpleUserDto(u.Id, u.DisplayName, u.Email, u.DepartmentId, u.Department?.Name))
                        .ToList();
    }

    // ─── Program attachment ───────────────────────────────────────────────────

    public async Task<ErrorOr<CourseOfferingDto>> AttachProgramAsync(
        Guid offeringId, Guid programId, Guid levelId, CancellationToken ct = default)
    {
        var offering = await OfferingsWithNavigations()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        if (offering is null)
            return Error.NotFound("CourseOffering.NotFound", "Course offering not found.");

        var alreadyAttached = offering.Programs.Any(p =>
            p.ProgramId == programId && p.LevelId == levelId);

        if (alreadyAttached)
            return Error.Conflict("CourseOfferingProgram.Duplicate",
                "This program/level combination is already attached to the offering.");

        dbContext.CourseOfferingPrograms.Add(new CourseOfferingProgram
        {
            CourseOfferingId = offeringId,
            ProgramId        = programId,
            LevelId          = levelId
        });

        await dbContext.SaveChangesAsync(ct);
        await LogActionAsync("AttachProgram", "CourseOffering", offeringId.ToString(),
            $"Attached program {programId} / level {levelId}", ct);

        // Reload
        var updated = await OfferingsWithNavigations()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        return updated!.ToDto();
    }

    public async Task<ErrorOr<CourseOfferingDto>> DetachProgramAsync(
        Guid offeringId, Guid programId, Guid levelId, CancellationToken ct = default)
    {
        var row = await dbContext.CourseOfferingPrograms
            .FirstOrDefaultAsync(p =>
                p.CourseOfferingId == offeringId &&
                p.ProgramId == programId &&
                p.LevelId == levelId, ct);

        if (row is null)
            return Error.NotFound("CourseOfferingProgram.NotFound", "Program attachment not found.");

        dbContext.CourseOfferingPrograms.Remove(row);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("DetachProgram", "CourseOffering", offeringId.ToString(),
            $"Detached program {programId} / level {levelId}", ct);

        var updated = await OfferingsWithNavigations()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        return updated!.ToDto();
    }

    // ─── Lecturer assignment ──────────────────────────────────────────────────

    public async Task<ErrorOr<CourseOfferingDto>> AssignLecturerAsync(
        Guid offeringId,
        Guid? lecturerId,
        List<Guid>? coLecturerIds,
        CancellationToken ct = default)
    {
        var offering = await OfferingsWithNavigations()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        if (offering is null)
            return Error.NotFound("CourseOffering.NotFound", "Course offering not found.");

        // Remove all existing lecturer assignments
        var existing = await dbContext.CourseOfferingLecturers
            .Where(l => l.CourseOfferingId == offeringId)
            .ToListAsync(ct);
        dbContext.CourseOfferingLecturers.RemoveRange(existing);

        var notifyList = new List<(AppUser user, CourseLecturerRole role)>();

        // Add primary
        if (lecturerId.HasValue)
        {
            var lecturer = await userRepository.GetByIdAsync(lecturerId.Value, ct);
            if (lecturer is null) return Error.NotFound("Lecturer.NotFound", "Main lecturer not found.");

            dbContext.CourseOfferingLecturers.Add(new CourseOfferingLecturer
            {
                CourseOfferingId = offeringId,
                LecturerId       = lecturerId.Value,
                Role             = CourseLecturerRole.Main
            });
            notifyList.Add((lecturer, CourseLecturerRole.Main));
        }

        // Add co-lecturers (skip duplicates / same as primary)
        foreach (var coId in (coLecturerIds ?? []).Distinct().Where(id => id != lecturerId))
        {
            var co = await userRepository.GetByIdAsync(coId, ct);
            if (co is null) continue;

            dbContext.CourseOfferingLecturers.Add(new CourseOfferingLecturer
            {
                CourseOfferingId = offeringId,
                LecturerId       = coId,
                Role             = CourseLecturerRole.CoLecturer
            });
            notifyList.Add((co, CourseLecturerRole.CoLecturer));
        }

        await dbContext.SaveChangesAsync(ct);
        await LogActionAsync("AssignLecturer", "CourseOffering", offeringId.ToString(),
            $"Assigned lecturer(s) to {offering.Course.Code}", ct);

        var sessionName = offering.AcademicSession?.Name ?? "the academic session";
        foreach (var (user, role) in notifyList)
            await NotifyLecturerAssignedAsync(user, offering.Course, sessionName, role, ct);

        var updated = await OfferingsWithNavigations()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);
        return updated!.ToDto();
    }

    public async Task<ErrorOr<List<CourseOfferingDto>>> GetCourseOfferingsAsync(
        Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var query = OfferingsWithNavigations().AsQueryable();

        if (academicSessionId.HasValue)
            query = query.Where(co => co.AcademicSessionId == academicSessionId.Value);

        var offerings = await query
            .OrderBy(co => co.Course.Code)
            .ToListAsync(ct);

        return offerings.Select(co => co.ToDto()).ToList();
    }

    public async Task<ErrorOr<BulkAssignLecturersResult>> BulkAssignLecturersAsync(
        List<OfferingAssignment> assignments, CancellationToken ct = default)
    {
        if (assignments is null || assignments.Count == 0)
            return Error.Validation("Assignments.Empty", "At least one offering assignment is required.");

        var offeringIds = assignments.Select(a => a.OfferingId).Distinct().ToList();
        var updated  = new List<CourseOfferingDto>();
        var errors   = new List<string>();

        foreach (var assignment in assignments)
        {
            var result = await AssignLecturerAsync(
                assignment.OfferingId,
                assignment.LecturerId,
                assignment.CoLecturerIds,
                ct);

            if (result.IsError)
                errors.Add($"Offering {assignment.OfferingId}: {result.FirstError.Description}");
            else
                updated.Add(result.Value);
        }

        await LogActionAsync("BulkAssignLecturers", "CourseOffering",
            string.Join(",", offeringIds),
            $"Bulk assigned lecturers for {updated.Count} offerings", ct);

        return new BulkAssignLecturersResult(updated, errors);
    }

    // ─── My Courses (Lecturer dashboard) ─────────────────────────────────────

    public async Task<ErrorOr<LecturerCoursesResponse>> GetMyCoursesAsync(
        Guid lecturerId,
        bool isAdmin = false,
        Guid? academicSessionId = null,
        CancellationToken ct = default)
    {
        IQueryable<CourseOfferingLecturer> query = dbContext.CourseOfferingLecturers
            .AsNoTracking()
            .Include(col => col.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(col => col.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .Include(col => col.CourseOffering)
                .ThenInclude(co => co.Programs)
                    .ThenInclude(p => p.Program)
            .Include(col => col.CourseOffering)
                .ThenInclude(co => co.Programs)
                    .ThenInclude(p => p.Level);

        if (!isAdmin)
            query = query.Where(col => col.LecturerId == lecturerId);

        if (academicSessionId.HasValue)
            query = query.Where(col => col.CourseOffering.AcademicSessionId == academicSessionId.Value);

        var lecturerRows = await query.ToListAsync(ct);

        // Admins: show all offerings once (not per-lecturer-assignment)
        if (isAdmin)
        {
            var allOfferings = await OfferingsWithNavigations()
                .Where(co => !academicSessionId.HasValue || co.AcademicSessionId == academicSessionId.Value)
                .OrderBy(co => co.Course.Code)
                .ToListAsync(ct);

            var allOfferingIds = allOfferings.Select(co => co.Id).ToList();
            var publishedIds = (await dbContext.GradePublications
                .Where(gp => allOfferingIds.Contains(gp.CourseOfferingId) && gp.IsVisibleToStudents)
                .Select(gp => gp.CourseOfferingId)
                .ToListAsync(ct)).ToHashSet();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var studentCounts = await dbContext.CourseEnrollments
                .Where(e => allOfferingIds.Contains(e.CourseOfferingId) && e.Status == "Registered")
                .GroupBy(e => e.CourseOfferingId)
                .Select(g => new { OfferingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OfferingId, x => x.Count, ct);

            var sessionCounts = await dbContext.LectureSessions
                .Where(ls => allOfferingIds.Contains(ls.CourseOfferingId) && ls.SessionDate >= today)
                .GroupBy(ls => ls.CourseOfferingId)
                .Select(g => new { OfferingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OfferingId, x => x.Count, ct);

            var offeringDtos = new List<LecturerCourseOfferingDto>();
            int totalStudents = 0;

            foreach (var co in allOfferings)
            {
                var studentCount = studentCounts.GetValueOrDefault(co.Id, 0);
                var sessionCount = sessionCounts.GetValueOrDefault(co.Id, 0);
                totalStudents += studentCount;

                offeringDtos.Add(BuildLecturerCourseOfferingDto(
                    co, CourseLecturerRole.Main, studentCount, sessionCount, publishedIds.Contains(co.Id)));
            }

            return new LecturerCoursesResponse(offeringDtos, offeringDtos.Count, totalStudents);
        }

        // Lecturer: each CourseOfferingLecturer row = one card on dashboard
        var dtos = new List<LecturerCourseOfferingDto>();
        int lecturerTotalStudents = 0;

        var lecturerOfferingIds = lecturerRows.Select(r => r.CourseOffering.Id).Distinct().ToList();
        var lecturerPublishedIds = (await dbContext.GradePublications
            .Where(gp => lecturerOfferingIds.Contains(gp.CourseOfferingId) && gp.IsVisibleToStudents)
            .Select(gp => gp.CourseOfferingId)
            .ToListAsync(ct)).ToHashSet();

        var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var lecturerStudentCounts = await dbContext.CourseEnrollments
            .Where(e => lecturerOfferingIds.Contains(e.CourseOfferingId) && e.Status == "Registered")
            .GroupBy(e => e.CourseOfferingId)
            .Select(g => new { OfferingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OfferingId, x => x.Count, ct);

        var lecturerSessionCounts = await dbContext.LectureSessions
            .Where(ls => lecturerOfferingIds.Contains(ls.CourseOfferingId) && ls.SessionDate >= todayDate)
            .GroupBy(ls => ls.CourseOfferingId)
            .Select(g => new { OfferingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OfferingId, x => x.Count, ct);

        foreach (var row in lecturerRows)
        {
            var co = row.CourseOffering;
            var studentCount = lecturerStudentCounts.GetValueOrDefault(co.Id, 0);
            var sessionCount = lecturerSessionCounts.GetValueOrDefault(co.Id, 0);
            lecturerTotalStudents += studentCount;

            dtos.Add(BuildLecturerCourseOfferingDto(co, row.Role, studentCount, sessionCount, lecturerPublishedIds.Contains(co.Id)));
        }

        return new LecturerCoursesResponse(dtos, dtos.Count, lecturerTotalStudents);
    }

    // ─── Course detail (Lecturer-facing) ─────────────────────────────────────

    public async Task<ErrorOr<CourseDetailResponse>> GetCourseDetailAsync(
        Guid offeringId, Guid lecturerId, bool bypassLecturerCheck = false, CancellationToken ct = default)
    {
        if (!bypassLecturerCheck)
        {
            var isAssigned = await dbContext.CourseOfferingLecturers
                .AnyAsync(col => col.CourseOfferingId == offeringId && col.LecturerId == lecturerId, ct);

            if (!isAssigned)
                return Error.NotFound("Course.NotFound",
                    "Course offering not found or you don't have access to it.");
        }

        var offering = await OfferingsWithNavigations()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        if (offering is null)
            return Error.NotFound("Course.NotFound", "Course offering not found.");

        var materials = await dbContext.CourseMaterials
            .AsNoTracking()
            .Where(cm => cm.CourseOfferingId == offeringId)
            .Include(cm => cm.UploadedBy)
            .OrderByDescending(cm => cm.UploadedAt)
            .Select(cm => new CourseMaterialDto(
                cm.Id, cm.Title, cm.Description, cm.FileUrl, cm.FileType, cm.FileSize,
                cm.UploadedAt,
                cm.UploadedBy.DisplayName ?? cm.UploadedBy.Email ?? "Unknown"))
            .ToListAsync(ct);

        var enrollments = await dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOfferingId == offeringId && e.Status == "Registered")
            .Include(e => e.Student)
            .OrderBy(e => e.Student.DisplayName)
            .ToListAsync(ct);

        var students = enrollments.Select(e => new CourseStudentDto(
            e.Student.Id,
            e.Student.Id.ToString()[..8],
            e.Student.DisplayName ?? e.Student.Email ?? "Unknown",
            e.Student.Email ?? "N/A",
            e.RegisteredAtUtc,
            null)).ToList();

        var programs  = offering.Programs.Select(p => new OfferingProgramDto(
            p.ProgramId, p.Program?.Name ?? "N/A", p.LevelId, p.Level?.Name ?? "N/A")).ToList();
        var lecturers = offering.Lecturers.Select(l => new OfferingLecturerDto(
            l.LecturerId, l.Lecturer?.DisplayName, l.Role)).ToList();

        return new CourseDetailResponse(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.Course.Description,
            offering.Course.CreditUnits,
            programs,
            offering.AcademicSessionId,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            lecturers,
            materials,
            students,
            materials.Count,
            students.Count);
    }

    // ─── Course materials ─────────────────────────────────────────────────────

    public async Task<ErrorOr<AddCourseMaterialResponse>> AddCourseMaterialAsync(
        Guid offeringId, Guid lecturerId, AddCourseMaterialRequest request, CancellationToken ct = default)
    {
        // Verify the lecturer is assigned to this offering
        var isAssigned = await dbContext.CourseOfferingLecturers
            .AnyAsync(col => col.CourseOfferingId == offeringId && col.LecturerId == lecturerId, ct);

        var offering = await dbContext.CourseOfferings
            .Include(co => co.Course)
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        if (!isAssigned || offering is null)
            return Error.NotFound("Course.NotFound", "Course offering not found or you don't have access to it.");

        string fileUrl;
        string? fileType;
        long? fileSize;

        if (!string.IsNullOrWhiteSpace(request.LinkUrl))
        {
            fileUrl  = request.LinkUrl.Trim();
            fileType = "Link";
            fileSize = null;
        }
        else
        {
            if (request.File == null || request.File.Length == 0)
                return Error.Validation("File.Required", "Please select a file or enter a link URL.");

            var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";
            fileUrl  = await fileStorageService.UploadFileAsync(
                request.File, $"course-materials/{offeringId}", fileName);
            fileType = request.File.ContentType;
            fileSize = request.File.Length;
        }

        var material = new CourseMaterial
        {
            CourseOfferingId = offeringId,
            Title            = request.Title,
            Description      = request.Description,
            FileUrl          = fileUrl,
            FileType         = fileType,
            FileSize         = fileSize,
            UploadedById     = lecturerId,
            UploadedAt       = DateTime.UtcNow
        };

        dbContext.CourseMaterials.Add(material);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("AddMaterial", "CourseMaterial", material.Id.ToString(),
            $"Added material '{request.Title}' to course {offering.Course.Code}", ct);

        // Notify enrolled students
        var enrolledStudentIds = await dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOfferingId == offeringId && e.Status == "Registered")
            .Select(e => e.StudentId)
            .ToListAsync(ct);

        foreach (var studentId in enrolledStudentIds)
        {
            await notificationService.CreateAsync(new CreateNotificationRequest(
                studentId, lecturerId,
                $"New Material: {request.Title}",
                $"New course material has been added to {offering.Course.Code}.",
                "System",
                $"/courses/{offeringId}/materials"), ct);
        }

        return new AddCourseMaterialResponse(material.Id, material.Title, material.FileUrl, material.UploadedAt);
    }

    public async Task<ErrorOr<Deleted>> DeleteCourseMaterialAsync(
        Guid materialId, Guid lecturerId, CancellationToken ct = default)
    {
        var material = await dbContext.CourseMaterials
            .Include(cm => cm.CourseOffering)
            .Include(cm => cm.CourseOffering.Course)
            .FirstOrDefaultAsync(cm => cm.Id == materialId, ct);

        if (material == null)
            return Error.NotFound("Material.NotFound", "Material not found.");

        // Verify the lecturer is assigned to this offering
        var isAssigned = await dbContext.CourseOfferingLecturers
            .AnyAsync(col => col.CourseOfferingId == material.CourseOfferingId
                          && col.LecturerId == lecturerId, ct);

        if (!isAssigned)
            return Error.Forbidden("Material.Forbidden",
                "You don't have permission to delete this material.");

        if (material.FileType != "Link")
            await fileStorageService.DeleteFileAsync(material.FileUrl);

        dbContext.CourseMaterials.Remove(material);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("DeleteMaterial", "CourseMaterial", materialId.ToString(),
            $"Deleted material '{material.Title}' from course {material.CourseOffering.Course.Code}", ct);

        return Result.Deleted;
    }

    // ─── Student course detail ────────────────────────────────────────────────

    public async Task<ErrorOr<StudentCourseDetailResponse>> GetStudentCourseDetailAsync(
        Guid offeringId, Guid studentId, CancellationToken ct = default)
    {
        var enrollment = await dbContext.CourseEnrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseOfferingId == offeringId
                                   && e.StudentId == studentId
                                   && e.Status != "Dropped", ct);

        var hasGrades = await dbContext.Grades
            .AnyAsync(g => g.StudentId == studentId && g.Assessment.CourseOfferingId == offeringId, ct);

        if (enrollment == null && !hasGrades)
            return Error.Forbidden("Enrollment.Forbidden", "You are not enrolled in this course.");

        var offering = await OfferingsWithNavigations()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        if (offering is null)
            return Error.NotFound("Course.NotFound", "Course offering not found.");

        // Use the first program/level for legacy display
        var firstProgram = offering.Programs.FirstOrDefault();
        var programName  = firstProgram?.Program?.Name ?? "N/A";
        var levelName    = firstProgram?.Level?.Name   ?? "N/A";

        var materials = await dbContext.CourseMaterials
            .AsNoTracking()
            .Where(cm => cm.CourseOfferingId == offeringId)
            .Include(cm => cm.UploadedBy)
            .OrderByDescending(cm => cm.UploadedAt)
            .Select(cm => new CourseMaterialDto(
                cm.Id, cm.Title, cm.Description, cm.FileUrl, cm.FileType, cm.FileSize,
                cm.UploadedAt,
                cm.UploadedBy.DisplayName ?? cm.UploadedBy.Email ?? "Unknown"))
            .ToListAsync(ct);

        var publication = await dbContext.GradePublications
            .AsNoTracking()
            .FirstOrDefaultAsync(gp => gp.CourseOfferingId == offeringId && gp.IsVisibleToStudents, ct);

        bool isPublished = publication != null;
        StudentCourseGradeDto? gradeDto   = null;
        CourseClassAnalyticsDto? analytics = null;

        if (isPublished)
        {
            var savedResult = await dbContext.StudentCourseResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.CourseOfferingId == offeringId && r.StudentId == studentId && r.IsPublished, ct);

            if (savedResult != null)
            {
                gradeDto = new StudentCourseGradeDto(
                    (double?)savedResult.Ca1Score,
                    (double?)savedResult.Ca2Score,
                    (double?)savedResult.Ca3Score,
                    (double?)savedResult.ExamScore,
                    (double)savedResult.TotalScore,
                    savedResult.LetterGrade,
                    (double)savedResult.GradePoints,
                    true);
            }
            else
            {
                var assessments = await dbContext.Assessments
                    .AsNoTracking()
                    .Where(a => a.CourseOfferingId == offeringId)
                    .Include(a => a.AssessmentCategory)
                    .Include(a => a.Grades.Where(g => g.StudentId == studentId))
                    .ToListAsync(ct);

                if (assessments.Any())
                {
                    var categories = await dbContext.AssessmentCategories
                        .AsNoTracking()
                        .Where(c => c.CourseOfferingId == offeringId)
                        .ToListAsync(ct);

                    var sysConfig = await dbContext.SystemGradingConfigurations
                        .AsNoTracking()
                        .OrderByDescending(x => x.UpdatedAt)
                        .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();

                    var calculated = gradeCalculationEngine.CalculateStudentGrade(
                        studentId,
                        assessments,
                        categories,
                        assessments.SelectMany(a => a.Grades).ToList(),
                        sysConfig);

                    gradeDto = new StudentCourseGradeDto(
                        (double?)calculated.Ca1Score,
                        (double?)calculated.Ca2Score,
                        (double?)calculated.Ca3Score,
                        (double?)calculated.ExamScore,
                        (double)calculated.TotalScore,
                        calculated.LetterGrade,
                        (double)calculated.GradePoints,
                        true);
                }
            }

            // Class analytics from published student results
            var publishedResults = await dbContext.StudentCourseResults
                .AsNoTracking()
                .Where(r => r.CourseOfferingId == offeringId && r.IsPublished)
                .ToListAsync(ct);

            if (publishedResults.Count > 1)
            {
                var scores = publishedResults.Select(r => (double)r.TotalScore).ToList();
                double classAverage = scores.Average();
                double? myScore = gradeDto?.TotalScore;

                var buckets = new List<ScoreBucketDto>();
                for (int start = 0; start < 100; start += 10)
                {
                    int end = start == 90 ? 100 : start + 9;
                    int count = scores.Count(s => s >= start && s <= end);
                    buckets.Add(new ScoreBucketDto(start, end, count));
                }

                int? percentile = null;
                if (myScore.HasValue && scores.Count > 0)
                {
                    int below = scores.Count(s => s < myScore.Value);
                    percentile = (int)Math.Round((double)below / scores.Count * 100);
                }

                analytics = new CourseClassAnalyticsDto(
                    Math.Round(classAverage, 1), myScore, percentile, scores.Count, buckets);
            }
        }

        return new StudentCourseDetailResponse(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.Course.Description,
            offering.Course.CreditUnits,
            programName,
            levelName,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            materials,
            materials.Count,
            gradeDto,
            analytics);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task NotifyLecturerAssignedAsync(
        AppUser lecturer, Course course, string sessionName,
        CourseLecturerRole role, CancellationToken ct)
    {
        var roleLabel = role == CourseLecturerRole.Main ? "Main Lecturer" : "Co-Lecturer";

        try
        {
            await notificationService.CreateAsync(new CreateNotificationRequest(
                lecturer.Id, null,
                "New Course Assignment",
                $"You have been assigned as {roleLabel} for {course.Code} – {course.Title} ({sessionName}).",
                "System",
                "/dashboard/lecturer/courses"), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create notification for lecturer {LecturerId} on course {CourseCode}",
                lecturer.Id, course.Code);
        }

        if (!string.IsNullOrEmpty(lecturer.Email))
        {
            try
            {
                await emailService.SendCourseAssignmentEmailAsync(
                    lecturer.Email, lecturer.DisplayName ?? "Lecturer",
                    course.Code, course.Title, sessionName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send assignment email to lecturer {LecturerId} ({Email})",
                    lecturer.Id, lecturer.Email);

                await LogActionAsync("EmailFailed", "CourseOfferingLecturer", lecturer.Id.ToString(),
                    $"Failed to send assignment email to lecturer {lecturer.DisplayName ?? "Lecturer"} ({lecturer.Email}) for {course.Code}: {ex.Message}", ct);
            }
        }
    }

    private static LecturerCourseOfferingDto BuildLecturerCourseOfferingDto(
        CourseOffering co, CourseLecturerRole role, int studentCount, int sessionCount, bool isPublished = false)
    {
        var programNames = string.Join(", ", co.Programs.Select(p => p.Program?.Name).Distinct());
        var levelNames   = string.Join(", ", co.Programs.Select(p => p.Level?.Name).Distinct());

        return new LecturerCourseOfferingDto(
            co.Id,
            co.CourseId,
            co.Course.Code,
            co.Course.Title,
            co.Course.CreditUnits,
            string.IsNullOrEmpty(programNames) ? "N/A" : programNames,
            string.IsNullOrEmpty(levelNames)   ? "N/A" : levelNames,
            co.AcademicSessionId,
            co.AcademicSession?.Name ?? "N/A",
            (int)co.Semester,
            role,
            studentCount,
            sessionCount,
            isPublished);
    }

    // ─── Admin Batch Registration ──────────────────────────────────────────────

    public async Task<ErrorOr<List<OfferingEnrolledStudentDto>>> GetOfferingEnrolledStudentsAsync(
        Guid offeringId, Guid? lecturerId = null, bool bypassLecturerCheck = true, CancellationToken ct = default)
    {
        if (!bypassLecturerCheck && lecturerId.HasValue)
        {
            var isAssigned = await dbContext.CourseOfferingLecturers
                .AnyAsync(col => col.CourseOfferingId == offeringId && col.LecturerId == lecturerId.Value, ct);

            if (!isAssigned)
                return Error.Forbidden("Course.NotAssigned", "You are not assigned to this course offering.");
        }

        var offering = await dbContext.CourseOfferings
            .AsNoTracking()
            .FirstOrDefaultAsync(co => co.Id == offeringId, ct);

        if (offering is null)
            return DomainErrors.Course.OfferingNotFound;

        var enrollments = await dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOfferingId == offeringId && e.Status == "Registered")
            .Include(e => e.Student)
            .OrderByDescending(e => e.RegisteredAtUtc)
            .ToListAsync(ct);

        var studentUserIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

        var students = await dbContext.Students
            .AsNoTracking()
            .Include(s => s.AcademicProgram)
            .Include(s => s.Level)
            .Where(s => studentUserIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var result = new List<OfferingEnrolledStudentDto>();
        foreach (var e in enrollments)
        {
            students.TryGetValue(e.StudentId, out var s);
            var firstName = s?.FirstName ?? e.Student?.DisplayName?.Split(' ').FirstOrDefault() ?? "Unknown";
            var lastName = s?.LastName ?? (e.Student?.DisplayName?.Split(' ').Length > 1 ? string.Join(" ", e.Student.DisplayName.Split(' ').Skip(1)) : "");
            var middleName = s?.MiddleName;
            var matricNumber = s?.StudentNumber ?? "N/A";
            var email = s?.OfficialEmail ?? e.Student?.Email ?? "";
            var programName = s?.AcademicProgram?.Name;
            var levelName = s?.Level?.Name;

            result.Add(new OfferingEnrolledStudentDto(
                e.StudentId,
                matricNumber,
                firstName,
                lastName,
                middleName,
                email,
                programName,
                levelName,
                e.Status,
                e.RegisteredAtUtc));
        }

        return result;
    }

    public async Task<ErrorOr<BatchRegistrationResultDto>> BatchRegisterStudentsAsync(
        BatchRegisterStudentsRequest request, Guid currentUserId, bool bypassLecturerCheck = false, CancellationToken ct = default)
    {
        if (request.CourseOfferingId == Guid.Empty)
        {
            return Error.Validation("CourseOfferingId.Required", "Course offering ID is required.");
        }

        if (request.StudentIds == null || request.StudentIds.Count == 0)
        {
            return Error.Validation("StudentIds.Required", "At least one student must be selected.");
        }

        if (!bypassLecturerCheck)
        {
            var isAssigned = await dbContext.CourseOfferingLecturers
                .AnyAsync(col => col.CourseOfferingId == request.CourseOfferingId && col.LecturerId == currentUserId, ct);

            if (!isAssigned)
            {
                return Error.Forbidden("Course.NotAssigned", "You are not assigned to this course offering.");
            }
        }

        var offering = await dbContext.CourseOfferings
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == request.CourseOfferingId, ct);

        if (offering is null)
            return DomainErrors.Course.OfferingNotFound;

        var studentIds = request.StudentIds.Distinct().ToList();

        // Query students
        var students = await dbContext.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Level)
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        // Fetch existing enrollments for this offering
        var existingEnrollments = await dbContext.CourseEnrollments
            .Where(e => e.CourseOfferingId == offering.Id && studentIds.Contains(e.StudentId))
            .ToDictionaryAsync(e => e.StudentId, ct);

        // Also fetch existing users to ensure foreign key constraint on CourseEnrollment.StudentId (AppUser)
        var existingUsers = await dbContext.Users
            .Where(u => studentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        // Get Student Role if needed
        var studentRoleId = await dbContext.Roles
            .Where(r => r.Name == "Student")
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        var results = new List<BatchRegistrationStudentResultDto>();
        int successfullyRegistered = 0;
        int alreadyRegistered = 0;
        int failed = 0;
        var now = DateTime.UtcNow;

        foreach (var studentId in studentIds)
        {
            if (!students.TryGetValue(studentId, out var student) && !existingUsers.ContainsKey(studentId))
            {
                failed++;
                results.Add(new BatchRegistrationStudentResultDto(
                    studentId,
                    "Unknown",
                    "N/A",
                    "Failed",
                    "Student record not found."));
                continue;
            }

            var studentName = student != null 
                ? $"{student.FirstName} {student.LastName}".Trim() 
                : (existingUsers.TryGetValue(studentId, out var u) ? u.DisplayName ?? u.Email : "Unknown");
            var matricNumber = student?.StudentNumber ?? "N/A";

            // Ensure AppUser exists for this studentId
            if (!existingUsers.ContainsKey(studentId) && student != null)
            {
                var newUser = new AppUser
                {
                    Id = student.Id,
                    EntraObjectId = string.IsNullOrWhiteSpace(student.EntraObjectId) ? $"student:{student.Id}" : student.EntraObjectId,
                    Username = !string.IsNullOrWhiteSpace(student.OfficialEmail) ? student.OfficialEmail : $"student_{student.Id:N}",
                    Email = !string.IsNullOrWhiteSpace(student.OfficialEmail) ? student.OfficialEmail : student.PersonalEmail,
                    DisplayName = studentName,
                    IsActive = true,
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                dbContext.Users.Add(newUser);
                existingUsers[student.Id] = newUser;

                if (studentRoleId.HasValue)
                {
                    dbContext.UserRoles.Add(new UserRole
                    {
                        UserId = newUser.Id,
                        RoleId = studentRoleId.Value,
                        AssignedUtc = now
                    });
                }
            }

            // Check existing enrollment
            if (existingEnrollments.TryGetValue(studentId, out var existingEnrollment))
            {
                if (existingEnrollment.Status == "Registered")
                {
                    alreadyRegistered++;
                    results.Add(new BatchRegistrationStudentResultDto(
                        studentId,
                        studentName,
                        matricNumber,
                        "AlreadyRegistered",
                        "Student is already registered for this course offering."));
                    continue;
                }

                // If Dropped or any other status, reinstate to Registered
                existingEnrollment.Status = "Registered";
                existingEnrollment.RegisteredAtUtc = now;
                existingEnrollment.DroppedAtUtc = null;
                existingEnrollment.UpdatedById = currentUserId;

                successfullyRegistered++;
                results.Add(new BatchRegistrationStudentResultDto(
                    studentId,
                    studentName,
                    matricNumber,
                    "Registered",
                    "Student registration reactivated."));
            }
            else
            {
                var newEnrollment = new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    CourseOfferingId = offering.Id,
                    Status = "Registered",
                    RegisteredAtUtc = now,
                    CreatedById = currentUserId
                };
                dbContext.CourseEnrollments.Add(newEnrollment);
                existingEnrollments[studentId] = newEnrollment;

                successfullyRegistered++;
                results.Add(new BatchRegistrationStudentResultDto(
                    studentId,
                    studentName,
                    matricNumber,
                    "Registered",
                    "Student successfully registered."));
            }
        }

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("BatchRegisterStudents", "CourseOffering", offering.Id.ToString(),
            $"Batch registered {successfullyRegistered} student(s) to offering {offering.Course.Code} ({offering.Id}) by user {currentUserId}", ct);

        return new BatchRegistrationResultDto(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.AcademicSession?.Name ?? "Unknown Session",
            (int)offering.Semester,
            studentIds.Count,
            successfullyRegistered,
            alreadyRegistered,
            failed,
            results);
    }

    public async Task<ErrorOr<BatchUnregisterResultDto>> BatchUnregisterStudentsAsync(
        BatchUnregisterStudentsRequest request, Guid currentUserId, bool bypassLecturerCheck = false, CancellationToken ct = default)
    {
        if (request.CourseOfferingId == Guid.Empty)
        {
            return Error.Validation("CourseOfferingId.Required", "Course offering ID is required.");
        }

        if (request.StudentIds == null || request.StudentIds.Count == 0)
        {
            return Error.Validation("StudentIds.Required", "At least one student must be selected.");
        }

        if (!bypassLecturerCheck)
        {
            var isAssigned = await dbContext.CourseOfferingLecturers
                .AnyAsync(col => col.CourseOfferingId == request.CourseOfferingId && col.LecturerId == currentUserId, ct);

            if (!isAssigned)
            {
                return Error.Forbidden("Course.NotAssigned", "You are not assigned to this course offering.");
            }
        }

        var offering = await dbContext.CourseOfferings
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == request.CourseOfferingId, ct);

        if (offering is null)
            return DomainErrors.Course.OfferingNotFound;

        var isPublished = await dbContext.GradePublications
            .AnyAsync(x => x.CourseOfferingId == offering.Id && x.IsVisibleToStudents, ct);
        if (isPublished)
        {
            return Error.Conflict("Registration.GradesPublished", "You cannot drop a course once its results have been published.");
        }

        var studentIds = request.StudentIds.Distinct().ToList();

        var students = await dbContext.Students
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var existingUsers = await dbContext.Users
            .Where(u => studentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var enrollments = await dbContext.CourseEnrollments
            .Where(e => e.CourseOfferingId == offering.Id && studentIds.Contains(e.StudentId))
            .ToDictionaryAsync(e => e.StudentId, ct);

        var results = new List<BatchUnregisterStudentResultDto>();
        int successfullyUnregistered = 0;
        int alreadyUnregistered = 0;
        int failed = 0;
        var now = DateTime.UtcNow;

        foreach (var studentId in studentIds)
        {
            students.TryGetValue(studentId, out var student);
            existingUsers.TryGetValue(studentId, out var user);

            var studentName = student != null 
                ? $"{student.FirstName} {student.LastName}".Trim() 
                : (user?.DisplayName ?? user?.Email ?? "Unknown");
            var matricNumber = student?.StudentNumber ?? "N/A";

            if (!enrollments.TryGetValue(studentId, out var enrollment))
            {
                alreadyUnregistered++;
                results.Add(new BatchUnregisterStudentResultDto(
                    studentId,
                    studentName,
                    matricNumber,
                    "NotEnrolled",
                    "Student is not enrolled in this course offering."));
                continue;
            }

            if (enrollment.Status == "Dropped")
            {
                alreadyUnregistered++;
                results.Add(new BatchUnregisterStudentResultDto(
                    studentId,
                    studentName,
                    matricNumber,
                    "AlreadyDropped",
                    "Student enrollment is already dropped."));
                continue;
            }

            enrollment.Status = "Dropped";
            enrollment.DroppedAtUtc = now;
            enrollment.UpdatedById = currentUserId;

            successfullyUnregistered++;
            results.Add(new BatchUnregisterStudentResultDto(
                studentId,
                studentName,
                matricNumber,
                "Dropped",
                "Student course enrollment successfully dropped."));
        }

        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("BatchUnregisterStudents", "CourseOffering", offering.Id.ToString(),
            $"Batch dropped/unregistered {successfullyUnregistered} student(s) from offering {offering.Course.Code} ({offering.Id}) by user {currentUserId}", ct);

        return new BatchUnregisterResultDto(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.AcademicSession?.Name ?? "Unknown Session",
            (int)offering.Semester,
            studentIds.Count,
            successfullyUnregistered,
            alreadyUnregistered,
            failed,
            results);
    }
}

