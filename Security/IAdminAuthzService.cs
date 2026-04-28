using LMS.Api.Data.Entities;

namespace LMS.Api.Security;

public interface IAdminAuthzService
{
    Task<ListManagedUsersResult> ListManagedUsersAsync(string? search, CancellationToken ct = default);
    Task<GetManagedUserResult> GetManagedUserAsync(string entraObjectId, CancellationToken ct = default);
    Task<SetManagedUserStatusResult> SetManagedUserStatusAsync(string entraObjectId, bool isActive, CancellationToken ct = default);
    Task<AssignUserRoleResult> AssignUserRoleAsync(string entraObjectId, string roleName, CancellationToken ct = default);
    Task<RevokeUserRoleResult> RevokeUserRoleAsync(string entraObjectId, string roleName, CancellationToken ct = default);
    Task<SetUserPermissionResult> SetUserPermissionAsync(
        string entraObjectId,
        string permissionCode,
        PermissionEffect effect,
        string? reason,
        DateTime? expiresUtc,
        CancellationToken ct = default);
    Task<GetEffectivePermissionsResult> GetEffectivePermissionsAsync(string entraObjectId, CancellationToken ct = default);
}

public sealed record ManagedUserSummary(
    string EntraObjectId,
    string? Email,
    string? DisplayName,
    string? Username,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyCollection<string> Roles);

public sealed record ManagedUserPermissionOverride(
    string PermissionCode,
    PermissionEffect Effect,
    string? Reason,
    DateTime ModifiedUtc,
    DateTime? ExpiresUtc,
    bool IsActive);

public sealed record ManagedUserDetail(
    string EntraObjectId,
    string? Email,
    string? DisplayName,
    string? Username,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<ManagedUserPermissionOverride> PermissionOverrides,
    IReadOnlyCollection<string> EffectivePermissions);

public sealed record ListManagedUsersResult(
    bool Success,
    IReadOnlyCollection<ManagedUserSummary>? Users = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);

public sealed record GetManagedUserResult(
    bool Success,
    ManagedUserDetail? User = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);

public sealed record SetManagedUserStatusResult(
    bool Success,
    string? EntraObjectId = null,
    bool IsActive = false,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);

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
    DateTime? ExpiresUtc = null,
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
