using LMS.Api.Data.Entities;

namespace LMS.Api.Security;

public interface IAdminAuthzService
{
    Task<AssignUserRoleResult> AssignUserRoleAsync(string entraObjectId, string roleName, CancellationToken ct = default);
    Task<RevokeUserRoleResult> RevokeUserRoleAsync(string entraObjectId, string roleName, CancellationToken ct = default);
    Task<SetUserPermissionResult> SetUserPermissionAsync(
        string entraObjectId,
        string permissionCode,
        PermissionEffect effect,
        string? reason,
        CancellationToken ct = default);
    Task<GetEffectivePermissionsResult> GetEffectivePermissionsAsync(string entraObjectId, CancellationToken ct = default);
}

public sealed record AssignUserRoleResult(
    bool Success,
    string? EntraObjectId = null,
    string? RoleName = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);

public sealed record RevokeUserRoleResult(
    bool Success,
    string? EntraObjectId = null,
    string? RoleName = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);

public sealed record SetUserPermissionResult(
    bool Success,
    string? EntraObjectId = null,
    string? PermissionCode = null,
    PermissionEffect Effect = PermissionEffect.Grant,
    string? Reason = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);

public sealed record GetEffectivePermissionsResult(
    bool Success,
    string? EntraObjectId = null,
    IReadOnlyCollection<string>? Permissions = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);
