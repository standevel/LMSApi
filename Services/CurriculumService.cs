using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Services;

public sealed class CurriculumService(
    ICurriculumRepository curriculumRepository,
    IAuditService auditService) : BaseService(auditService), ICurriculumService
{
    public async Task<ErrorOr<CurriculumDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var curriculum = await curriculumRepository.GetByIdAsync(id, ct);
        if (curriculum is null) return DomainErrors.Curriculum.NotFound;

        return curriculum.ToDto();
    }

    public async Task<ErrorOr<List<CurriculumSummaryDto>>> GetByProgramIdAsync(Guid programId, CancellationToken ct = default)
    {
        var curricula = await curriculumRepository.GetByProgramIdAsync(programId, ct);
        return curricula.Select(x => x.ToSummaryDto()).ToList();
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
}
