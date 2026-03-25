using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class UserRoleRepository(LmsDbContext dbContext) : IUserRoleRepository
{
    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default) =>
        await (from userRole in dbContext.UserRoles.AsNoTracking()
               join role in dbContext.Roles.AsNoTracking()
                  on userRole.RoleId equals role.Id
               where userRole.UserId == userId
               select role.Name)
            .Distinct()
            .ToListAsync(ct);

    public Task<bool> AssignmentExistsAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
        dbContext.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == roleId, ct);

    public Task AssignAsync(Guid userId, Guid roleId, DateTime assignedUtc, CancellationToken ct = default) =>
        dbContext.UserRoles.AddAsync(new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedUtc = assignedUtc
        }, ct).AsTask();

    public async Task<bool> RevokeAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var assignment = await dbContext.UserRoles.FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, ct);
        if (assignment is null)
        {
            return false;
        }

        dbContext.UserRoles.Remove(assignment);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => dbContext.SaveChangesAsync(ct);
}
