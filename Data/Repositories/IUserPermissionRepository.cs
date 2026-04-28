using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface IUserPermissionRepository
{
    Task<UserPermission?> GetAsync(Guid userId, Guid permissionId, CancellationToken ct = default);
    Task AddAsync(UserPermission userPermission, CancellationToken ct = default);
    void Remove(UserPermission userPermission);
    Task SaveChangesAsync(CancellationToken ct = default);
}
