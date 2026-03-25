using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface IAcademicProgramRepository
{
    Task<AcademicProgram?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<AcademicProgram>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(AcademicProgram program, CancellationToken ct = default);
    Task UpdateAsync(AcademicProgram program, CancellationToken ct = default);
    Task DeleteAsync(AcademicProgram program, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
