using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Security;

public sealed class AdminAuthzService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleRepository userRoleRepository,
    IPermissionRepository permissionRepository,
    IUserPermissionRepository userPermissionRepository,
    IPermissionService permissionService) : IAdminAuthzService
{
    public async Task<AssignUserRoleResult> AssignUserRoleAsync(string entraObjectId, string roleName, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            return new AssignUserRoleResult(false, ErrorCode: "user_not_found", ErrorMessage: "User was not found.", StatusCode: StatusCodes.Status404NotFound);
        }

        var role = await roleRepository.GetByNameAsync(roleName, ct);
        if (role is null)
        {
            return new AssignUserRoleResult(false, ErrorCode: "invalid_role", ErrorMessage: $"Role '{roleName}' does not exist.", StatusCode: StatusCodes.Status400BadRequest);
        }

        var exists = await userRoleRepository.AssignmentExistsAsync(user.Id, role.Id, ct);
        if (!exists)
        {
            await userRoleRepository.AssignAsync(user.Id, role.Id, DateTime.UtcNow, ct);
            await userRoleRepository.SaveChangesAsync(ct);
        }

        return new AssignUserRoleResult(true, user.EntraObjectId, role.Name, StatusCode: StatusCodes.Status200OK);
    }

    public async Task<RevokeUserRoleResult> RevokeUserRoleAsync(string entraObjectId, string roleName, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            return new RevokeUserRoleResult(false, ErrorCode: "user_not_found", ErrorMessage: "User was not found.", StatusCode: StatusCodes.Status404NotFound);
        }

        var role = await roleRepository.GetByNameAsync(roleName, ct);
        if (role is null)
        {
            return new RevokeUserRoleResult(false, ErrorCode: "invalid_role", ErrorMessage: $"Role '{roleName}' does not exist.", StatusCode: StatusCodes.Status400BadRequest);
        }

        var removed = await userRoleRepository.RevokeAsync(user.Id, role.Id, ct);
        if (removed)
        {
            await userRoleRepository.SaveChangesAsync(ct);
        }

        return new RevokeUserRoleResult(true, user.EntraObjectId, role.Name, StatusCode: StatusCodes.Status200OK);
    }

    public async Task<SetUserPermissionResult> SetUserPermissionAsync(string entraObjectId, string permissionCode, PermissionEffect effect, string? reason, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            return new SetUserPermissionResult(false, ErrorCode: "user_not_found", ErrorMessage: "User was not found.", StatusCode: StatusCodes.Status404NotFound);
        }

        var permission = await permissionRepository.GetByCodeAsync(permissionCode, ct);
        if (permission is null)
        {
            return new SetUserPermissionResult(false, ErrorCode: "invalid_permission", ErrorMessage: $"Permission '{permissionCode}' does not exist.", StatusCode: StatusCodes.Status400BadRequest);
        }

        var record = await userPermissionRepository.GetAsync(user.Id, permission.Id, ct);
        if (record is null)
        {
            record = new UserPermission
            {
                UserId = user.Id,
                PermissionId = permission.Id
            };
            await userPermissionRepository.AddAsync(record, ct);
        }

        record.Effect = effect;
        record.Reason = reason;
        record.ModifiedUtc = DateTime.UtcNow;
        await userPermissionRepository.SaveChangesAsync(ct);

        return new SetUserPermissionResult(
            true,
            EntraObjectId: user.EntraObjectId,
            PermissionCode: permission.Code,
            Effect: effect,
            Reason: reason,
            StatusCode: StatusCodes.Status200OK);
    }

    public async Task<GetEffectivePermissionsResult> GetEffectivePermissionsAsync(string entraObjectId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            return new GetEffectivePermissionsResult(false, ErrorCode: "user_not_found", ErrorMessage: "User was not found.", StatusCode: StatusCodes.Status404NotFound);
        }

        var permissions = await permissionService.GetEffectivePermissionsAsync(user.Id, ct);
        var sorted = permissions.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return new GetEffectivePermissionsResult(true, user.EntraObjectId, sorted, StatusCode: StatusCodes.Status200OK);
    }
}
