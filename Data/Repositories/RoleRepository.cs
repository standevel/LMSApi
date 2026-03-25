using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class RoleRepository(LmsDbContext dbContext) : IRoleRepository
{
    public Task<AppRole?> GetByNameAsync(string roleName, CancellationToken ct = default) =>
        dbContext.Roles.FirstOrDefaultAsync(x => x.Name == roleName, ct);
}
