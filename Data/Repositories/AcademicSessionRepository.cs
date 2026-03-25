using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class AcademicSessionRepository(LmsDbContext dbContext)
    : BaseRepository<AcademicSession>(dbContext), IAcademicSessionRepository
{
    public Task<AcademicSession?> GetActiveAsync(CancellationToken ct = default)
    {
        return DbSet.FirstOrDefaultAsync(x => x.IsActive, ct);
    }
}
