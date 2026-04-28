using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class CourseService(
    ICourseRepository courseRepository,
    IUserRepository userRepository,
    IAuditService auditService,
    LmsDbContext dbContext,
    IFileStorageService fileStorageService) : BaseService(auditService), ICourseService
{
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
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            Description = request.Description,
            CreditUnits = request.CreditUnits,
            IsActive = true,
            Offerings = request.Offerings.Select(o => new CourseOffering
            {
                ProgramId = o.ProgramId,
                LevelId = o.LevelId,
                AcademicSessionId = o.AcademicSessionId,
                LecturerId = o.LecturerId,
                Semester = (LMS.Api.Data.Enums.Semester)o.Semester
            }).ToList()
        };

        await courseRepository.AddAsync(course, ct);
        await courseRepository.SaveChangesAsync(ct);

        await LogActionAsync("Create", "Course", course.Id.ToString(), $"Created course: {course.Code} - {course.Title}", ct);

        var createdCourse = await courseRepository.GetByIdAsync(course.Id, ct);
        return createdCourse!.ToDto();
    }

    public async Task<ErrorOr<CourseDto>> UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken ct = default)
    {
        var course = await courseRepository.GetByIdAsync(id, ct);
        if (course == null) return DomainErrors.Course.NotFound;

        course.Code = request.Code;
        course.Title = request.Title;
        course.Description = request.Description;
        course.CreditUnits = request.CreditUnits;

        course.Offerings.Clear();
        foreach (var o in request.Offerings)
        {
            course.Offerings.Add(new CourseOffering
            {
                CourseId = id,
                ProgramId = o.ProgramId,
                LevelId = o.LevelId,
                AcademicSessionId = o.AcademicSessionId,
                LecturerId = o.LecturerId,
                Semester = (LMS.Api.Data.Enums.Semester)o.Semester
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

        await LogActionAsync("ToggleStatus", "Course", id.ToString(), $"Toggled status for course {course.Code} to {course.IsActive}", ct);

        return course.ToDto();
    }

    public async Task<ErrorOr<List<SimpleUserDto>>> GetLecturersAsync(CancellationToken ct = default)
    {
        var lecturers = await userRepository.GetByRoleAsync("Lecturer", ct);
        return lecturers.Select(u => new SimpleUserDto(u.Id, u.DisplayName, u.Email)).ToList();
    }

    public async Task<ErrorOr<LecturerCoursesResponse>> GetMyCoursesAsync(Guid lecturerId, CancellationToken ct = default)
    {
        // Get all course offerings for this lecturer with related data
        var offerings = await dbContext.CourseOfferings
            .AsNoTracking()
            .Where(co => co.LecturerId == lecturerId)
            .Include(co => co.Course)
            .Include(co => co.Program)
            .Include(co => co.Level)
            .Include(co => co.AcademicSession)
            .OrderBy(co => co.Course.Code)
            .ToListAsync(ct);

        if (!offerings.Any())
        {
            return new LecturerCoursesResponse(
                new List<LecturerCourseOfferingDto>(),
                0,
                0);
        }

        // Get student counts for each offering
        var offeringDtos = new List<LecturerCourseOfferingDto>();
        int totalStudents = 0;

        foreach (var offering in offerings)
        {
            // Count enrolled students matching program + level + session
            var studentCount = await dbContext.Enrollments
                .CountAsync(e => e.ProgramId == offering.ProgramId
                    && e.LevelId == offering.LevelId
                    && e.AcademicSessionId == offering.AcademicSessionId, ct);

            // Count upcoming lecture sessions for this course offering
            var sessionCount = await dbContext.LectureSessions
                .CountAsync(ls => ls.CourseOfferingId == offering.Id
                    && ls.SessionDate >= DateOnly.FromDateTime(DateTime.UtcNow), ct);

            totalStudents += studentCount;

            offeringDtos.Add(new LecturerCourseOfferingDto(
                offering.Id,
                offering.CourseId,
                offering.Course.Code,
                offering.Course.Title,
                offering.Course.CreditUnits,
                offering.ProgramId,
                offering.Program.Name,
                offering.LevelId,
                offering.Level.Name,
                offering.AcademicSessionId,
                offering.AcademicSession.Name,
                (int)offering.Semester,
                studentCount,
                sessionCount));
        }

        return new LecturerCoursesResponse(
            offeringDtos,
            offeringDtos.Count,
            totalStudents);
    }

    public async Task<ErrorOr<CourseDetailResponse>> GetCourseDetailAsync(Guid offeringId, Guid lecturerId, CancellationToken ct = default)
    {
        // Verify the offering exists and belongs to this lecturer
        var offering = await dbContext.CourseOfferings
            .AsNoTracking()
            .Include(co => co.Course)
            .Include(co => co.Program)
            .Include(co => co.Level)
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == offeringId && co.LecturerId == lecturerId, ct);

        if (offering == null)
        {
            return Error.NotFound("Course.NotFound", "Course offering not found or you don't have access to it.");
        }

        // Get materials for this offering
        var materials = await dbContext.CourseMaterials
            .AsNoTracking()
            .Where(cm => cm.CourseOfferingId == offeringId)
            .Include(cm => cm.UploadedBy)
            .OrderByDescending(cm => cm.UploadedAt)
            .Select(cm => new CourseMaterialDto(
                cm.Id,
                cm.Title,
                cm.Description,
                cm.FileUrl,
                cm.FileType,
                cm.FileSize,
                cm.UploadedAt,
                cm.UploadedBy.DisplayName ?? cm.UploadedBy.Email ?? "Unknown"))
            .ToListAsync(ct);

        // Get students enrolled in this program + level + session
        var enrollments = await dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.ProgramId == offering.ProgramId
                && e.LevelId == offering.LevelId
                && e.AcademicSessionId == offering.AcademicSessionId)
            .Include(e => e.User)
            .OrderBy(e => e.User.DisplayName)
            .ToListAsync(ct);

        var students = enrollments.Select(e => new CourseStudentDto(
            e.User.Id,
            e.User.Id.ToString().Substring(0, 8), // Use part of GUID as student identifier
            e.User.DisplayName ?? e.User.Email ?? "Unknown",
            e.User.Email ?? "N/A",
            e.EnrolledAtUtc,
            null)).ToList();

        return new CourseDetailResponse(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.Course.Description,
            offering.Course.CreditUnits,
            offering.ProgramId,
            offering.Program.Name,
            offering.LevelId,
            offering.Level.Name,
            offering.AcademicSessionId,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            materials,
            students,
            materials.Count,
            students.Count);
    }

    public async Task<ErrorOr<AddCourseMaterialResponse>> AddCourseMaterialAsync(Guid offeringId, Guid lecturerId, AddCourseMaterialRequest request, CancellationToken ct = default)
    {
        // Verify the offering exists and belongs to this lecturer
        var offering = await dbContext.CourseOfferings
            .Include(co => co.Course)
            .FirstOrDefaultAsync(co => co.Id == offeringId && co.LecturerId == lecturerId, ct);

        if (offering == null)
        {
            return Error.NotFound("Course.NotFound", "Course offering not found or you don't have access to it.");
        }

        if (request.File == null || request.File.Length == 0)
        {
            return Error.Validation("File.Required", "Please select a file to upload.");
        }

        // Upload file using FileStorageService
        var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";
        var fileUrl = await fileStorageService.UploadFileAsync(
            request.File,
            $"course-materials/{offeringId}",
            fileName);

        var material = new CourseMaterial
        {
            CourseOfferingId = offeringId,
            Title = request.Title,
            Description = request.Description,
            FileUrl = fileUrl,
            FileType = request.File.ContentType,
            FileSize = request.File.Length,
            UploadedById = lecturerId,
            UploadedAt = DateTime.UtcNow
        };

        dbContext.CourseMaterials.Add(material);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("AddMaterial", "CourseMaterial", material.Id.ToString(), 
            $"Added material '{request.Title}' to course {offering.Course.Code}", ct);

        return new AddCourseMaterialResponse(
            material.Id,
            material.Title,
            material.FileUrl,
            material.UploadedAt);
    }

    public async Task<ErrorOr<Deleted>> DeleteCourseMaterialAsync(Guid materialId, Guid lecturerId, CancellationToken ct = default)
    {
        var material = await dbContext.CourseMaterials
            .Include(cm => cm.CourseOffering)
            .Include(cm => cm.CourseOffering.Course)
            .FirstOrDefaultAsync(cm => cm.Id == materialId, ct);

        if (material == null)
        {
            return Error.NotFound("Material.NotFound", "Material not found.");
        }

        // Verify the lecturer owns this course
        if (material.CourseOffering.LecturerId != lecturerId)
        {
            return Error.Forbidden("Material.Forbidden", "You don't have permission to delete this material.");
        }

        // Delete file from storage
        await fileStorageService.DeleteFileAsync(material.FileUrl);

        dbContext.CourseMaterials.Remove(material);
        await dbContext.SaveChangesAsync(ct);

        await LogActionAsync("DeleteMaterial", "CourseMaterial", materialId.ToString(), 
            $"Deleted material '{material.Title}' from course {material.CourseOffering.Course.Code}", ct);

        return Result.Deleted;
    }
}
