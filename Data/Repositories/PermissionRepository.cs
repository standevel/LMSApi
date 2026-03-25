using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class PermissionRepository(LmsDbContext dbContext) : IPermissionRepository
{
    public Task<AppPermission?> GetByCodeAsync(string permissionCode, CancellationToken ct = default) =>
        dbContext.Permissions.FirstOrDefaultAsync(x => x.Code == permissionCode, ct);
}
