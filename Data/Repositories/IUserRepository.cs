using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<AppUser?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default);
    Task<AppUser?> GetActiveByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, Guid? excludingUserId = null, CancellationToken ct = default);
    Task<Guid?> GetIdByEntraObjectIdAsync(string entraObjectId, CancellationToken ct = default);
    Task<Guid?> GetIdBySubjectAsync(Guid subjectUserId, CancellationToken ct = default);
    Task<List<AppUser>> GetByRoleAsync(string roleName, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
