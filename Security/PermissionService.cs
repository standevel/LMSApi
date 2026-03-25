using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Security;

public sealed class PermissionService(LmsDbContext dbContext) : IPermissionService
{
    public async Task<HashSet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var rolePermissionCodes = await
            (from userRole in dbContext.UserRoles.AsNoTracking()
             join rolePermission in dbContext.RolePermissions.AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
             join permission in dbContext.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
             where userRole.UserId == userId
             select permission.Code)
            .Distinct()
            .ToListAsync(ct);

        var effective = rolePermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var userOverrides = await
            (from userPermission in dbContext.UserPermissions.AsNoTracking()
             join permission in dbContext.Permissions.AsNoTracking()
                on userPermission.PermissionId equals permission.Id
             where userPermission.UserId == userId
             select new { permission.Code, userPermission.Effect })
            .ToListAsync(ct);

        foreach (var userOverride in userOverrides)
        {
            if (userOverride.Effect == PermissionEffect.Grant)
            {
                effective.Add(userOverride.Code);
            }
            else
            {
                effective.Remove(userOverride.Code);
            }
        }

        return effective;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default)
    {
        var permissions = await GetEffectivePermissionsAsync(userId, ct);
        return permissions.Contains(permissionCode);
    }
}
