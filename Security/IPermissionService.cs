namespace LMS.Api.Security;

public interface IPermissionService
{
    Task<HashSet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default);
}
