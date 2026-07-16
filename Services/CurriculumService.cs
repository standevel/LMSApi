using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Data.Entities;
using LMS.Api.Data;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class CurriculumService(
    LmsDbContext dbContext,
    ICurriculumRepository curriculumRepository,
    ICurrentUserContext currentUserContext,
    IUserRoleRepository userRoleRepository,
    IAuditService auditService) : BaseService(auditService), ICurriculumService
{
    private async Task<bool> IsUserAdminAsync(Guid userId, CancellationToken ct)
    {
        var roles = await userRoleRepository.GetRoleNamesAsync(userId, ct);
        return roles.Contains(LmsRoles.SuperAdmin) || roles.Contains(LmsRoles.Admin) || roles.Contains(LmsRoles.AcademicAdmin) || roles.Contains(LmsRoles.ViceChancellor);
    }

    private async Task<bool> IsUserDeanAsync(Guid userId, CancellationToken ct)
    {
        var roles = await userRoleRepository.GetRoleNamesAsync(userId, ct);
        return roles.Contains(LmsRoles.Dean);
    }

    private async Task<bool> IsUserHodAsync(Guid userId, CancellationToken ct)
    {
        var roles = await userRoleRepository.GetRoleNamesAsync(userId, ct);
        return roles.Contains(LmsRoles.HOD);
    }

    private async Task<bool> CanUserAccessCurriculumAsync(Curriculum curriculum, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct) ?? Guid.Empty;
        if (userId == Guid.Empty) return false;
        
        if (await IsUserAdminAsync(userId, ct)) return true;

        bool isDean = await IsUserDeanAsync(userId, ct);
        bool isHod = await IsUserHodAsync(userId, ct);

        if (isDean && curriculum.Program?.Department?.Faculty?.DeanId == userId) return true;
        if (isHod && curriculum.Program?.Department?.HeadId == userId) return true;

        return false;
    }

    public async Task<ErrorOr<CurriculumDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(id, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        if (!await CanUserAccessCurriculumAsync(curriculum, ct))
            return Error.Forbidden("Curriculum.AccessDenied", "You do not have permission to access this curriculum.");

        return curriculum.ToDto();
    }

    public async Task<ErrorOr<List<CurriculumSummaryDto>>> GetByProgramIdAsync(Guid programId, CancellationToken ct = default)
    {
        var curricula = await curriculumRepository.GetByProgramIdAsync(programId, ct);
        
        var filteredCurricula = new List<Curriculum>();
        foreach (var curriculum in curricula)
        {
            if (await CanUserAccessCurriculumAsync(curriculum, ct))
            {
                filteredCurricula.Add(curriculum);
            }
        }

        return filteredCurricula.Select(x => x.ToSummaryDto()).ToList();
    }

    public async Task<ErrorOr<CurriculumDto>> CreateCurriculumAsync(Guid programId, CreateCurriculumRequest request, CancellationToken ct = default)
    {
        var curriculum = new Curriculum
        {
            ProgramId = programId,
            AdmissionSessionId = request.AdmissionSessionId,
            Name = request.Name,
            MinCreditUnitsForGraduation = request.MinCreditUnitsForGraduation,
            IsActive = true
        };

        await curriculumRepository.AddAsync(curriculum, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Curriculum", curriculum.Id.ToString(), $"Created curriculum: {curriculum.Name}", ct);

        var result = await curriculumRepository.GetByIdAsync(curriculum.Id, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<CurriculumDto>> UpdateCurriculumAsync(Guid curriculumId, UpdateCurriculumRequest request, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var oldName = curriculum.Name;
        curriculum.Name = request.Name;
        curriculum.MinCreditUnitsForGraduation = request.MinCreditUnitsForGraduation;
        curriculum.AdmissionSessionId = request.AdmissionSessionId;

        await curriculumRepository.SaveChangesAsync(ct);
        await LogActionAsync("Update", "Curriculum", curriculum.Id.ToString(), $"Updated curriculum name from '{oldName}' to '{request.Name}'", ct);

        var result = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<CurriculumDto>> AddCourseAsync(Guid curriculumId, AddCurriculumCourseRequest request, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var isDuplicate = curriculum.Courses.Any(c =>
            c.LevelId == request.LevelId &&
            c.CourseId == request.CourseId &&
            c.Semester == (LMS.Api.Data.Enums.Semester)request.Semester);

        if (isDuplicate)
            return DomainErrors.Curriculum.DuplicateCourse;

        var course = new CurriculumCourse
        {
            CurriculumId = curriculumId,
            LevelId = request.LevelId,
            CourseId = request.CourseId,
            Semester = (LMS.Api.Data.Enums.Semester)request.Semester,
            Category = (LMS.Api.Data.Enums.CourseCategory)request.Category,
            CreditUnits = request.CreditUnits
        };

        await curriculumRepository.AddCourseAsync(course, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("AddCourse", "Curriculum", curriculumId.ToString(), $"Added course {request.CourseId} to curriculum", ct);

        var result = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<CurriculumDto>> UpdateCourseAsync(Guid curriculumId, Guid id, UpdateCurriculumCourseRequest request, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var course = curriculum.Courses.FirstOrDefault(c => c.Id == id);
        if (course is null) return Error.NotFound("CurriculumCourse.NotFound", "The curriculum course was not found.");

        var oldUnits = course.CreditUnits;
        course.LevelId = request.LevelId;
        course.Semester = (LMS.Api.Data.Enums.Semester)request.Semester;
        course.Category = (LMS.Api.Data.Enums.CourseCategory)request.Category;
        course.CreditUnits = request.CreditUnits;

        await curriculumRepository.UpdateCourseAsync(course, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("UpdateCourse", "Curriculum", curriculumId.ToString(), $"Updated course {course.CourseId} in curriculum. Units: {oldUnits} -> {request.CreditUnits}", ct);

        var result = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<CurriculumDto>> AddCoursesBulkAsync(Guid curriculumId, BulkAddCurriculumCourseRequest request, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var addedCount = 0;
        foreach (var selection in request.Selections)
        {
            var isDuplicate = curriculum.Courses.Any(c =>
                c.LevelId == request.LevelId &&
                c.CourseId == selection.CourseId &&
                c.Semester == (LMS.Api.Data.Enums.Semester)request.Semester);

            if (isDuplicate) continue;

            var course = new CurriculumCourse
            {
                CurriculumId = curriculumId,
                LevelId = request.LevelId,
                CourseId = selection.CourseId,
                Semester = (LMS.Api.Data.Enums.Semester)request.Semester,
                Category = (LMS.Api.Data.Enums.CourseCategory)selection.Category,
                CreditUnits = selection.CreditUnits
            };

            await curriculumRepository.AddCourseAsync(course, ct);
            addedCount++;
        }

        if (addedCount > 0)
        {
            await curriculumRepository.SaveChangesAsync(ct);
            await LogActionAsync("BulkAddCourses", "Curriculum", curriculumId.ToString(), $"Batch added {addedCount} courses to curriculum", ct);
        }

        var result = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<EnrollmentDto>> EnrollStudentAsync(EnrollStudentRequest request, CancellationToken ct = default)
    {
        var existing = await curriculumRepository.GetEnrollmentAsync(request.StudentId, request.AcademicSessionId, ct);
        if (existing != null) return DomainErrors.Enrollment.Duplicate;

        var enrollment = new ProgramEnrollment
        {
            ProgramId = request.ProgramId,
            LevelId = request.LevelId,
            UserId = request.StudentId,
            AcademicSessionId = request.AcademicSessionId,
            CurriculumId = request.CurriculumId
        };

        await curriculumRepository.AddEnrollmentAsync(enrollment, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("Enroll", "Student", request.StudentId.ToString(), $"Enrolled student in Program {request.ProgramId}", ct);

        // Map response (In a real app, you'd fetch the full graph here or handle mapping better)
        // For brevity, fetching via Repo again
        var reloaded = await curriculumRepository.GetByIdAsync(request.CurriculumId, ct); // Just to verify connections

        // This is a bit complex for a repo mapping, usually we'd have a separate EnrollmentRepo
        return new EnrollmentDto(
            enrollment.Id,
            enrollment.ProgramId,
            "", // Fetched later or handled by UI
            enrollment.LevelId,
            "",
            enrollment.UserId,
            "",
            enrollment.AcademicSessionId,
            "",
            enrollment.CurriculumId,
            "",
            enrollment.EnrolledAtUtc);
    }

    public async Task<ErrorOr<CurriculumDto>> CloneCurriculumAsync(Guid curriculumId, string newName, CancellationToken ct = default)
    {
        var source = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (source is null) return DomainErrors.Curriculum.NotFound;

        var clone = new Curriculum
        {
            ProgramId = source.ProgramId,
            AdmissionSessionId = source.AdmissionSessionId,
            Name = newName,
            MinCreditUnitsForGraduation = source.MinCreditUnitsForGraduation,
            Status = CurriculumStatus.Draft,
            ParentCurriculumId = curriculumId,
            IsActive = true,
            Courses = source.Courses.Select(c => new CurriculumCourse
            {
                LevelId = c.LevelId,
                CourseId = c.CourseId,
                Semester = c.Semester,
                Category = c.Category,
                CreditUnits = c.CreditUnits
            }).ToList()
        };

        await curriculumRepository.AddAsync(clone, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("Clone", "Curriculum", clone.Id.ToString(), $"Cloned curriculum from {source.Name} to {newName}", ct);

        var result = await curriculumRepository.GetByIdAsync(clone.Id, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<CurriculumDto>> PublishCurriculumAsync(Guid curriculumId, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        if (curriculum.Status == CurriculumStatus.Published) return curriculum.ToDto();

        // Validate before publishing
        var validation = await ValidatePrerequisitesAsync(curriculumId, ct);
        if (validation.IsError) return validation.Errors;

        curriculum.Status = CurriculumStatus.Published;
        await curriculumRepository.UpdateAsync(curriculum, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("Publish", "Curriculum", curriculumId.ToString(), $"Published curriculum {curriculum.Name}", ct);

        return curriculum.ToDto();
    }

    public async Task<ErrorOr<Deleted>> DeleteCurriculumAsync(Guid curriculumId, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        if (curriculum.Status == CurriculumStatus.Published)
            return Error.Validation("Curriculum.IsPublished", "Cannot delete a published curriculum.");

        var hasEnrollments = await dbContext.Enrollments.AnyAsync(e => e.CurriculumId == curriculumId, ct);
        if (hasEnrollments)
            return Error.Validation("Curriculum.HasEnrollments", "Cannot delete a curriculum with enrolled students.");

        await curriculumRepository.DeleteAsync(curriculum, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "Curriculum", curriculumId.ToString(), $"Deleted curriculum: {curriculum.Name}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<bool>> ValidatePrerequisitesAsync(Guid curriculumId, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var courses = curriculum.Courses.Select(c => c.CourseId).ToList();

        foreach (var courseId in courses)
        {
            var visited = new HashSet<Guid>();
            var path = new List<Guid>();
            if (await HasCircularDependency(courseId, visited, path, ct))
            {
                // In a real app, you'd return a more specific error with the path
                return Error.Failure("Prerequisite.CircularDependency", $"Circular dependency detected starting from course {courseId}");
            }
        }

        return true;
    }

    private async Task<bool> HasCircularDependency(Guid courseId, HashSet<Guid> visited, List<Guid> path, CancellationToken ct)
    {
        if (path.Contains(courseId)) return true; // Cycle detected
        if (visited.Contains(courseId)) return false; // Already checked

        visited.Add(courseId);
        path.Add(courseId);

        var prerequisites = await curriculumRepository.GetPrerequisitesAsync(courseId, ct);
        foreach (var prereq in prerequisites)
        {
            if (await HasCircularDependency(prereq.PrerequisiteCourseId, visited, new List<Guid>(path), ct))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<ErrorOr<bool>> AddPrerequisiteAsync(Guid courseId, AddCoursePrerequisiteRequest request, CancellationToken ct = default)
    {
        // Prevent immediate circularity
        if (courseId == request.PrerequisiteCourseId)
            return Error.Validation("Prerequisite.Invalid", "A course cannot be a prerequisite of itself.");

        // Check if adding this will cause a cycle
        var visited = new HashSet<Guid>();
        var path = new List<Guid> { courseId };
        if (await HasCircularDependency(request.PrerequisiteCourseId, visited, path, ct))
        {
            return Error.Failure("Prerequisite.CircularDependency", "Adding this prerequisite would create a circular dependency.");
        }

        var prerequisite = new CoursePrerequisite
        {
            CourseId = courseId,
            PrerequisiteCourseId = request.PrerequisiteCourseId,
            Type = request.Type
        };

        await curriculumRepository.AddPrerequisiteAsync(prerequisite, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("AddPrerequisite", "Course", courseId.ToString(), $"Added prerequisite {request.PrerequisiteCourseId}", ct);

        return true;
    }

    public async Task<ErrorOr<List<CurriculumHistoryDto>>> GetHistoryAsync(Guid curriculumId, CancellationToken ct = default)
    {
        var logs = await curriculumRepository.GetAuditLogsAsync("Curriculum", curriculumId.ToString(), ct);
        return logs.Select(x => new CurriculumHistoryDto(
            x.Id,
            x.Action,
            x.Changes,
            x.User?.DisplayName ?? "System",
            x.Timestamp)).ToList();
    }

    public async Task<ErrorOr<CurriculumDto>> RemoveLevelAsync(Guid curriculumId, Guid levelId, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var coursesToRemove = curriculum.Courses.Where(x => x.LevelId == levelId).ToList();
        if (coursesToRemove.Count > 0)
        {
            foreach (var cc in coursesToRemove)
            {
                await curriculumRepository.DeleteCourseAsync(cc, ct);
            }
            await curriculumRepository.SaveChangesAsync(ct);

            var levelName = coursesToRemove.First().Level?.Name ?? levelId.ToString();
            await LogActionAsync("DeleteLevel", "Curriculum", curriculum.Id.ToString(), $"Removed level {levelName} and all its mapped courses from curriculum", ct);
        }

        var result = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<CurriculumDto>> RemoveCourseAsync(Guid curriculumId, Guid id, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var course = curriculum.Courses.FirstOrDefault(c => c.Id == id);
        if (course is null) return Error.NotFound("CurriculumCourse.NotFound", "The curriculum course mapping was not found.");

        await curriculumRepository.DeleteCourseAsync(course, ct);
        await curriculumRepository.SaveChangesAsync(ct);

        await LogActionAsync("RemoveCourse", "Curriculum", curriculumId.ToString(), $"Removed course {course.Course?.Code ?? course.CourseId.ToString()} from curriculum", ct);

        var result = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        return result!.ToDto();
    }

    public async Task<ErrorOr<RemoveCourseConsequencesDto>> GetRemoveCourseConsequencesAsync(Guid curriculumId, Guid id, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(curriculumId, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        var courseMapping = curriculum.Courses.FirstOrDefault(c => c.Id == id);
        if (courseMapping is null) return Error.NotFound("CurriculumCourse.NotFound", "The curriculum course mapping was not found.");

        // Check for active offerings
        var offerings = await dbContext.CourseOfferings
            .Where(co => co.CurriculumId == curriculumId && co.CourseId == courseMapping.CourseId)
            .ToListAsync(ct);

        var offeringIds = offerings.Select(o => o.Id).ToList();
        var enrolledCount = 0;
        var hasGrades = false;

        if (offeringIds.Count > 0)
        {
            enrolledCount = await dbContext.CourseEnrollments
                .CountAsync(ce => offeringIds.Contains(ce.CourseOfferingId) && ce.Status == "Registered", ct);

            // Check if there are any grades for assessments in these offerings
            hasGrades = await dbContext.Grades
                .AnyAsync(g => dbContext.Assessments
                    .Where(a => offeringIds.Contains(a.CourseOfferingId))
                    .Select(a => a.Id)
                    .Contains(g.AssessmentId), ct);
        }

        // Check if it is a prerequisite for other courses
        var isPrerequisiteForOthers = await dbContext.CoursePrerequisites
            .AnyAsync(cp => cp.PrerequisiteCourseId == courseMapping.CourseId, ct);

        var dependentCourseNames = new List<string>();
        if (isPrerequisiteForOthers)
        {
            var dependentCourseIds = await dbContext.CoursePrerequisites
                .Where(cp => cp.PrerequisiteCourseId == courseMapping.CourseId)
                .Select(cp => cp.CourseId)
                .ToListAsync(ct);

            dependentCourseNames = await dbContext.Courses
                .Where(c => dependentCourseIds.Contains(c.Id))
                .Select(c => $"{c.Code} - {c.Title}")
                .ToListAsync(ct);
        }

        return new RemoveCourseConsequencesDto(
            offerings.Count > 0,
            offerings.Count,
            enrolledCount,
            hasGrades,
            isPrerequisiteForOthers,
            dependentCourseNames);
    }
}
