using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class CourseRepository(LmsDbContext dbContext) : ICourseRepository
{
    public Task<Course?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        dbContext.Courses
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Program)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Level)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.AcademicSession)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Lecturer)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<Course>> GetAllAsync(CancellationToken ct = default) =>
        dbContext.Courses
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Program)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Level)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.AcademicSession)
            .Include(x => x.Offerings)
                .ThenInclude(x => x.Lecturer)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

    public async Task AddAsync(Course course, CancellationToken ct = default)
    {
        await dbContext.Courses.AddAsync(course, ct);
    }

    public Task UpdateAsync(Course course, CancellationToken ct = default)
    {
        if (dbContext.Entry(course).State == EntityState.Detached)
        {
            dbContext.Courses.Update(course);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Course course, CancellationToken ct = default)
    {
        dbContext.Courses.Remove(course);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => dbContext.SaveChangesAsync(ct);
}
