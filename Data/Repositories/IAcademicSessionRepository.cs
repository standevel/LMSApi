using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface IAcademicSessionRepository
{
    Task<AcademicSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<AcademicSession>> GetAllAsync(CancellationToken ct = default);
    Task<AcademicSession?> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(AcademicSession session, CancellationToken ct = default);
    Task UpdateAsync(AcademicSession session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
