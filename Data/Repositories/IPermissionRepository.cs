using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Repositories;

public interface IPermissionRepository
{
    Task<AppPermission?> GetByCodeAsync(string permissionCode, CancellationToken ct = default);
}
