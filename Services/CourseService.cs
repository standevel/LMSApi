using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Services;

public sealed class CourseService(
    ICourseRepository courseRepository,
    IUserRepository userRepository,
    IAuditService auditService) : BaseService(auditService), ICourseService
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
}
