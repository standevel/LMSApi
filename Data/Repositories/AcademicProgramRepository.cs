using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class AcademicProgramRepository(LmsDbContext dbContext)
    : BaseRepository<AcademicProgram>(dbContext), IAcademicProgramRepository
{
    public override Task<AcademicProgram?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return DbSet
            .Include(x => x.Faculty)
            .Include(x => x.Levels)
                .ThenInclude(l => l.Semesters)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public override Task<List<AcademicProgram>> GetAllAsync(CancellationToken ct = default)
    {
        return DbSet
            .Include(x => x.Faculty)
            .Include(x => x.Levels)
                .ThenInclude(l => l.Semesters)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
    {
        return DbSet.AnyAsync(x => x.Code == code, ct);
    }
}
