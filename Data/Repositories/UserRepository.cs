using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Data.Repositories;

public sealed class UserRepository(LmsDbContext dbContext) : IUserRepository
{
    public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

    public Task<AppUser?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default) =>
        dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.EntraObjectId == entraObjectId, ct);

    public Task<AppUser?> GetManagementProfileByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default) =>
        dbContext.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .Include(x => x.UserPermissions)
                .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.EntraObjectId == entraObjectId, ct);

    public Task<AppUser?> GetActiveByUsernameAsync(string username, CancellationToken ct = default) =>
        dbContext.Users.FirstOrDefaultAsync(x => (x.Username == username || x.Email == username) && x.IsActive, ct);


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

    public async Task<List<AppUser>> ListForManagementAsync(string? search, CancellationToken ct = default)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(x =>
                (x.DisplayName != null && x.DisplayName.Contains(normalizedSearch)) ||
                (x.Email != null && x.Email.Contains(normalizedSearch)) ||
                (x.Username != null && x.Username.Contains(normalizedSearch)) ||
                x.EntraObjectId.Contains(normalizedSearch));
        }

        var users = await query.ToListAsync(ct);

        return users
            .OrderBy(x => x.DisplayName ?? x.Email ?? x.Username ?? x.EntraObjectId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(user, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => dbContext.SaveChangesAsync(ct);
}
