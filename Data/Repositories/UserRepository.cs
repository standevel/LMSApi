using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class UserRepository(LmsDbContext dbContext) : IUserRepository
{
    public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);

    public Task<AppUser?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default) =>
        dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.EntraObjectId == entraObjectId, ct);

    public Task<AppUser?> GetActiveByUsernameAsync(string username, CancellationToken ct = default) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Username == username && x.IsActive, ct);

    public Task<bool> UsernameExistsAsync(string username, Guid? excludingUserId = null, CancellationToken ct = default) =>
        dbContext.Users.AnyAsync(x => x.Username == username && (!excludingUserId.HasValue || x.Id != excludingUserId.Value), ct);

    public Task<Guid?> GetIdByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default) =>
        dbContext.Users
            .AsNoTracking()
            .Where(x => x.EntraObjectId == entraObjectId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public Task<Guid?> GetIdBySubjectAsync(Guid subjectUserId, CancellationToken ct = default) =>
        dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == subjectUserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public Task<List<AppUser>> GetByRoleAsync(string roleName, CancellationToken ct = default) =>
        dbContext.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == roleName))
            .ToListAsync(ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(user, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => dbContext.SaveChangesAsync(ct);
}
