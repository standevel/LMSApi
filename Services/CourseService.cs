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
    IEmailService emailService) : BaseService(auditService), ICourseService
{
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
        return course.ToDto();
    }

    public async Task<ErrorOr<List<CourseDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var courses = await courseRepository.GetAllAsync(ct);
        return courses.Select(c => c.ToDto()).ToList();
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

        // Check for duplicate course code within the same program
        var existingCourse = await dbContext.Courses
            .FirstOrDefaultAsync(c => c.ProgramId == resolvedProgramId && c.Code == request.Code, ct);
        if (existingCourse != null)
        {
            return Error.Conflict("Course.DuplicateCode", $"Course code '{request.Code}' already exists for the selected program.");
        }

        var course = new Course
        {
            ProgramId   = resolvedProgramId,
            Code        = request.Code,
            Title       = request.Title,
            Description = request.Description,
            CreditUnits = request.CreditUnits,
            LevelId     = request.LevelId,
            Semester    = request.Semester,
            IsActive    = true,
            // Offerings with session, semester, and optional program+level
            Offerings = request.Offerings.Select(o => new CourseOffering
            {
                AcademicSessionId = o.AcademicSessionId,
                Semester          = (Semester)o.Semester
            }).ToList()
        };

        await courseRepository.AddAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        // Attach programs to offerings if provided in the request
        var attachmentErrors = new List<string>();
        foreach (var offeringRequest in request.Offerings.Where(o => o.ProgramId.HasValue && o.LevelId.HasValue))
        {
            var offering = course.Offerings.FirstOrDefault(o => o.AcademicSessionId == offeringRequest.AcademicSessionId && o.Semester == (Semester)offeringRequest.Semester);
            if (offering == null)
            {
                attachmentErrors.Add($"No offering found for session {offeringRequest.AcademicSessionId}, semester {offeringRequest.Semester}");
                continue;
            }

            var attachResult = await AttachProgramAsync(offering.Id, offeringRequest.ProgramId.GetValueOrDefault(), offeringRequest.LevelId.GetValueOrDefault(), ct);
            if (attachResult.IsError)
            {
                attachmentErrors.Add($"Failed to attach program {offeringRequest.ProgramId} to offering: {attachResult.FirstError.Description}");
            }
        }

        if (attachmentErrors.Any())
        {
            // Course was created but some attachments failed - return partial success with warnings
            await LogActionAsync("Create", "Course", course.Id.ToString(),
                $"Created course: {course.Code} - {course.Title} with attachment warnings: {string.Join("; ", attachmentErrors)}", ct);
        }
        else
        {
            await LogActionAsync("Create", "Course", course.Id.ToString(),
                $"Created course: {course.Code} - {course.Title}", ct);
        }

        var createdCourse = await courseRepository.GetByIdAsync(course.Id, ct);
        return createdCourse!.ToDto();
    }

    public async Task<ErrorOr<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        course.Code        = request.Code;
        course.Title       = request.Title;
        course.Description = request.Description;
        course.CreditUnits = request.CreditUnits;
        course.LevelId     = request.LevelId;
        course.Semester    = request.Semester;

        var existingOfferings = course.Offerings.ToList();
        var requestOfferings  = request.Offerings.ToList();

        // Remove offerings no longer in the request (matched by session+semester)
        foreach (var existing in existingOfferings)
        {
            var matched = requestOfferings.FirstOrDefault(r =>
                r.AcademicSessionId == existing.AcademicSessionId &&
                (Semester)r.Semester == existing.Semester);

            if (matched == null)
                dbContext.CourseOfferings.Remove(existing);
            else
                requestOfferings.Remove(matched);
        }

        // Add new offerings
        foreach (var req in requestOfferings)
        {
            dbContext.CourseOfferings.Add(new CourseOffering
            {
                CourseId          = id,
                AcademicSessionId = req.AcademicSessionId,
                Semester          = (Semester)req.Semester
            });
        }

        await courseRepository.UpdateAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("Update", "Course", id.ToString(), $"Updated course: {course.Code}", ct);

        var updatedCourse = await courseRepository.GetByIdAsync(id, ct);
        return updatedCourse!.ToDto();
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

        var hasAssignments = await dbContext.Assignments.AnyAsync(x => x.CourseId == id, ct);
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

            var offeringDtos = new List<LecturerCourseOfferingDto>();
            int totalStudents = 0;

            foreach (var co in allOfferings)
            {
                var studentCount = await dbContext.CourseEnrollments
                    .CountAsync(e => e.CourseOfferingId == co.Id && e.Status == "Registered", ct);
                var sessionCount = await dbContext.LectureSessions
                    .CountAsync(ls => ls.CourseOfferingId == co.Id
                        && ls.SessionDate >= DateOnly.FromDateTime(DateTime.UtcNow), ct);
                totalStudents += studentCount;

                offeringDtos.Add(BuildLecturerCourseOfferingDto(
                    co, CourseLecturerRole.Main, studentCount, sessionCount));
            }

            return new LecturerCoursesResponse(offeringDtos, offeringDtos.Count, totalStudents);
        }

        // Lecturer: each CourseOfferingLecturer row = one card on dashboard
        var dtos = new List<LecturerCourseOfferingDto>();
        int lecturerTotalStudents = 0;

        foreach (var row in lecturerRows)
        {
            var co = row.CourseOffering;
            var studentCount = await dbContext.CourseEnrollments
                .CountAsync(e => e.CourseOfferingId == co.Id && e.Status == "Registered", ct);
            var sessionCount = await dbContext.LectureSessions
                .CountAsync(ls => ls.CourseOfferingId == co.Id
                    && ls.SessionDate >= DateOnly.FromDateTime(DateTime.UtcNow), ct);
            lecturerTotalStudents += studentCount;

            dtos.Add(BuildLecturerCourseOfferingDto(co, row.Role, studentCount, sessionCount));
        }

        return new LecturerCoursesResponse(dtos, dtos.Count, lecturerTotalStudents);
    }

    // ─── Course detail (Lecturer-facing) ─────────────────────────────────────

    public async Task<ErrorOr<CourseDetailResponse>> GetCourseDetailAsync(
        Guid offeringId, Guid lecturerId, CancellationToken ct = default)
    {
        // Must be assigned to this offering (or admin will pass a dummy lecturerId)
        var isAssigned = await dbContext.CourseOfferingLecturers
            .AnyAsync(col => col.CourseOfferingId == offeringId && col.LecturerId == lecturerId, ct);

        if (!isAssigned)
            return Error.NotFound("Course.NotFound",
                "Course offering not found or you don't have access to it.");

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
                                   && e.Status == "Registered", ct);

        if (enrollment == null)
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
            var assessments = await dbContext.Assessments
                .AsNoTracking()
                .Where(a => a.CourseOfferingId == offeringId)
                .Include(a => a.AssessmentCategory)
                .Include(a => a.Grades.Where(g => g.StudentId == studentId))
                .ToListAsync(ct);

            if (assessments.Any())
            {
                double ca1Obtained = 0, ca1Max = 0;
                double ca2Obtained = 0, ca2Max = 0;
                double ca3Obtained = 0, ca3Max = 0;
                double examObtained = 0, examMax = 0;

                foreach (var assessment in assessments)
                {
                    var studentGrade = assessment.Grades.FirstOrDefault();
                    if (studentGrade == null) continue;

                    double maxMarks = (double)assessment.MaxMarks;
                    double obtained = (double)studentGrade.MarksObtained;

                    var catType = assessment.AssessmentCategory.CategoryType;
                    if (catType == AssessmentCategoryType.CA1)
                    {
                        ca1Obtained += obtained;
                        ca1Max += maxMarks;
                    }
                    else if (catType == AssessmentCategoryType.CA2)
                    {
                        ca2Obtained += obtained;
                        ca2Max += maxMarks;
                    }
                    else if (catType == AssessmentCategoryType.CA3)
                    {
                        ca3Obtained += obtained;
                        ca3Max += maxMarks;
                    }
                    else if (assessment.AssessmentCategory.IsExamCategory || catType == AssessmentCategoryType.Exam)
                    {
                        examObtained += obtained;
                        examMax += maxMarks;
                    }
                }

                double ca1 = ca1Max > 0 ? (ca1Obtained / ca1Max) * 100 : 0;
                double ca2 = ca2Max > 0 ? (ca2Obtained / ca2Max) * 100 : 0;
                double ca3 = ca3Max > 0 ? (ca3Obtained / ca3Max) * 100 : 0;
                double exam = examMax > 0 ? (examObtained / examMax) * 100 : 0;

                var sysConfig = await dbContext.SystemGradingConfigurations
                    .AsNoTracking()
                    .OrderByDescending(x => x.UpdatedAt)
                    .FirstOrDefaultAsync(ct);

                double ca1Weight = sysConfig != null ? (double)sysConfig.DefaultCA1Weight : 15.0;
                double ca2Weight = sysConfig != null ? (double)sysConfig.DefaultCA2Weight : 15.0;
                double ca3Weight = sysConfig != null ? (double)sysConfig.DefaultCA3Weight : 15.0;
                double examWeight = sysConfig != null ? (double)sysConfig.DefaultExamWeight : 55.0;

                double total = 0;
                if (sysConfig != null && sysConfig.DefaultGradingStyle == GradingStyle.Unweighted)
                {
                    var activeScores = new List<double>();
                    if (ca1Max > 0) activeScores.Add(ca1);
                    if (ca2Max > 0) activeScores.Add(ca2);
                    if (ca3Max > 0) activeScores.Add(ca3);
                    if (examMax > 0) activeScores.Add(exam);
                    total = activeScores.Any() ? activeScores.Average() : 0;
                }
                else
                {
                    total = (ca1 * ca1Weight / 100.0) +
                            (ca2 * ca2Weight / 100.0) +
                            (ca3 * ca3Weight / 100.0) +
                            (exam * examWeight / 100.0);
                }

                var mappings = string.IsNullOrEmpty(sysConfig?.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
                    ? new List<GradeMappingDto>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new List<GradeMappingDto>();

                var rStrategy = sysConfig?.RoundingStrategy ?? RoundingStrategy.Standard;
                var decimalPlaces = sysConfig?.RoundingDecimalPlaces ?? 0;
                var graceThreshold = sysConfig?.GraceThreshold ?? 0.0m;

                var gradeResult = GradeCalculator.CalculateGrade(
                    (decimal)total,
                    rStrategy,
                    decimalPlaces,
                    graceThreshold,
                    mappings);

                gradeDto = new StudentCourseGradeDto(
                    Math.Round(ca1, 2), Math.Round(ca2, 2), Math.Round(ca3, 2),
                    Math.Round(exam, 2), (double)gradeResult.Score, gradeResult.LetterGrade, (double)gradeResult.GradePoints, true);
            }

            // Class analytics
            var enrolledStudentIds = await dbContext.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.CourseOfferingId == offeringId && e.Status == "Registered")
                .Select(e => e.StudentId)
                .ToListAsync(ct);

            if (enrolledStudentIds.Count > 1)
            {
                var allAssessments = await dbContext.Assessments
                    .AsNoTracking()
                    .Where(a => a.CourseOfferingId == offeringId)
                    .Include(a => a.AssessmentCategory)
                    .Include(a => a.Grades.Where(g => enrolledStudentIds.Contains(g.StudentId)))
                    .ToListAsync(ct);

                var studentTotals = new Dictionary<Guid, double>();
                foreach (var sid in enrolledStudentIds)
                {
                    double t = 0;
                    foreach (var assessment in allAssessments)
                    {
                        var g = assessment.Grades.FirstOrDefault(gr => gr.StudentId == sid);
                        if (g == null) continue;
                        double maxM = (double)assessment.MaxMarks;
                        double w    = (double)assessment.AssessmentCategory.Weight;
                        if (maxM > 0) t += ((double)g.MarksObtained / maxM) * w;
                    }
                    studentTotals[sid] = Math.Round(t, 1);
                }

                var scores = studentTotals.Values.ToList();
                if (scores.Count > 0)
                {
                    double classAverage = scores.Average();
                    double? myScore     = studentTotals.TryGetValue(studentId, out var ms) ? ms : null;

                    var buckets = new List<ScoreBucketDto>();
                    for (int start = 0; start < 100; start += 10)
                    {
                        int end   = start == 90 ? 100 : start + 9;
                        int count = scores.Count(s => s >= start && s <= end);
                        buckets.Add(new ScoreBucketDto(start, end, count));
                    }

                    int? percentile = null;
                    if (myScore.HasValue)
                    {
                        int below = scores.Count(s => s < myScore.Value);
                        percentile = (int)Math.Round((double)below / scores.Count * 100);
                    }

                    analytics = new CourseClassAnalyticsDto(
                        Math.Round(classAverage, 1), myScore, percentile, scores.Count, buckets);
                }
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

        await notificationService.CreateAsync(new CreateNotificationRequest(
            lecturer.Id, null,
            "New Course Assignment",
            $"You have been assigned as {roleLabel} for {course.Code} – {course.Title} ({sessionName}).",
            "System",
            "/dashboard/lecturer/courses"), ct);

        if (!string.IsNullOrEmpty(lecturer.Email))
            await emailService.SendCourseAssignmentEmailAsync(
                lecturer.Email, lecturer.DisplayName ?? "Lecturer",
                course.Code, course.Title, sessionName);
    }

    private static LecturerCourseOfferingDto BuildLecturerCourseOfferingDto(
        CourseOffering co, CourseLecturerRole role, int studentCount, int sessionCount)
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
            sessionCount);
    }
}
