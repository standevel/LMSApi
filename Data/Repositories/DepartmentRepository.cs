using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Data.Repositories;

public sealed class DepartmentRepository(LmsDbContext dbContext)
    : BaseRepository<Department>(dbContext), IDepartmentRepository
{
    public override async Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(d => d.Faculty)
            .Include(d => d.Head)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public override async Task<List<Department>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet
            .Include(d => d.Faculty)
            .Include(d => d.Head)
            .ToListAsync(ct);
    }
}
