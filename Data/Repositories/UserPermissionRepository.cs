using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class UserPermissionRepository(LmsDbContext dbContext) : IUserPermissionRepository
{
    public Task<UserPermission?> GetAsync(Guid userId, Guid permissionId, CancellationToken ct = default) =>
        dbContext.UserPermissions.FirstOrDefaultAsync(x => x.UserId == userId && x.PermissionId == permissionId, ct);

    public Task AddAsync(UserPermission userPermission, CancellationToken ct = default) =>
        dbContext.UserPermissions.AddAsync(userPermission, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => dbContext.SaveChangesAsync(ct);
}
