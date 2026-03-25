using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class CurriculumRepository(LmsDbContext dbContext)
    : BaseRepository<Curriculum>(dbContext), ICurriculumRepository
{
    public override Task<Curriculum?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return DbSet
            .Include(x => x.Program)
            .Include(x => x.AdmissionSession)
            .Include(x => x.Courses).ThenInclude(c => c.Course)
            .Include(x => x.Courses).ThenInclude(c => c.Level)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<List<Curriculum>> GetByProgramIdAsync(Guid programId, CancellationToken ct = default)
    {
        return DbSet
            .Include(x => x.AdmissionSession)
            .Where(x => x.ProgramId == programId)
            .ToListAsync(ct);
    }



    public Task<CurriculumCourse?> GetCourseAsync(Guid curriculumId, Guid courseId, CancellationToken ct = default)
    {
        return DbContext.CurriculumCourses
            .Include(x => x.Course)
            .Include(x => x.Level)
            .FirstOrDefaultAsync(x => x.CurriculumId == curriculumId && x.CourseId == courseId, ct);
    }

    public Task AddCourseAsync(CurriculumCourse curriculumCourse, CancellationToken ct = default)
    {
        DbContext.CurriculumCourses.Add(curriculumCourse);
        return Task.CompletedTask;
    }

    public Task UpdateCourseAsync(CurriculumCourse curriculumCourse, CancellationToken ct = default)
    {
        DbContext.CurriculumCourses.Update(curriculumCourse);
        return Task.CompletedTask;
    }

    public Task DeleteCourseAsync(CurriculumCourse curriculumCourse, CancellationToken ct = default)
    {
        DbContext.CurriculumCourses.Remove(curriculumCourse);
        return Task.CompletedTask;
    }

    public Task<ProgramEnrollment?> GetEnrollmentAsync(Guid studentId, Guid sessionId, CancellationToken ct = default)
    {
        return DbContext.Enrollments
            .FirstOrDefaultAsync(x => x.UserId == studentId && x.AcademicSessionId == sessionId, ct);
    }

    public async Task AddEnrollmentAsync(ProgramEnrollment enrollment, CancellationToken ct = default)
    {
        await DbContext.Enrollments.AddAsync(enrollment, ct);
    }

    public Task<List<CoursePrerequisite>> GetPrerequisitesAsync(Guid courseId, CancellationToken ct = default)
    {
        return DbContext.CoursePrerequisites
            .Include(x => x.PrerequisiteCourse)
            .Where(x => x.CourseId == courseId)
            .ToListAsync(ct);
    }

    public Task AddPrerequisiteAsync(CoursePrerequisite prerequisite, CancellationToken ct = default)
    {
        DbContext.CoursePrerequisites.Add(prerequisite);
        return Task.CompletedTask;
    }

    public async Task DeletePrerequisiteAsync(Guid id, CancellationToken ct = default)
    {
        var prereq = await DbContext.CoursePrerequisites.FindAsync([id], ct);
        if (prereq != null)
        {
            DbContext.CoursePrerequisites.Remove(prereq);
        }
    }

    public Task<List<AuditLog>> GetAuditLogsAsync(string entityName, string entityId, CancellationToken ct = default)
    {
        return DbContext.AuditLogs
            .Include(x => x.User)
            .Where(x => x.EntityName == entityName && x.EntityId == entityId)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(ct);
    }
}
