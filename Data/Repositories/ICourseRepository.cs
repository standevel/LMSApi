using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Course>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Course course, CancellationToken ct = default);
    Task UpdateAsync(Course course, CancellationToken ct = default);
    Task DeleteAsync(Course course, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
