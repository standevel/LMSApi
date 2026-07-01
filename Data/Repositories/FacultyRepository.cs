using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Data.Repositories;

public sealed class FacultyRepository(LmsDbContext dbContext)
    : BaseRepository<Faculty>(dbContext), IFacultyRepository
{
    public override async Task<Faculty?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(f => f.Dean)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public override async Task<List<Faculty>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet
            .Include(f => f.Dean)
            .ToListAsync(ct);
    }
}
