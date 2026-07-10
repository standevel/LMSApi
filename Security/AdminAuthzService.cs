using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Security;

public sealed class AdminAuthzService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUserRoleRepository userRoleRepository,
    IPermissionRepository permissionRepository,
    IUserPermissionRepository userPermissionRepository,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    LmsDbContext dbContext) : IAdminAuthzService
{
    public async Task<ListManagedUsersResult> ListManagedUsersAsync(string? search, CancellationToken ct = default)
    {
        var users = await userRepository.ListForManagementAsync(search, ct);
        var userEmails = users
            .Select(x => x.Email)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToArray();
        var userObjectIds = users
            .Select(x => x.EntraObjectId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToArray();

        var studentMarkers = await dbContext.Students
            .AsNoTracking()
            .Where(student =>
                (student.EntraObjectId != null && userObjectIds.Contains(student.EntraObjectId)) ||
                userEmails.Contains(student.OfficialEmail))
            .Select(student => new
            {
                student.EntraObjectId,
                student.OfficialEmail
            })
            .ToListAsync(ct);
        var studentObjectIds = studentMarkers
            .Where(x => !string.IsNullOrWhiteSpace(x.EntraObjectId))
            .Select(x => x.EntraObjectId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var studentEmails = studentMarkers
            .Where(x => !string.IsNullOrWhiteSpace(x.OfficialEmail))
            .Select(x => x.OfficialEmail)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var payload = users
            .Select(user => new ManagedUserSummary(
                user.Id,
                user.EntraObjectId,
                user.Email,
                user.DisplayName,
                user.Username,
                user.IsActive,
                user.CreatedUtc,
                user.UpdatedUtc,
                user.UserRoles
                    .Select(x => x.Role.Name)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                user.DepartmentId,
                user.Department?.Name,
                IsStudentUser(user, studentObjectIds, studentEmails),
                IsLecturerUser(user)))
            .ToArray();

        return new ListManagedUsersResult(true, payload, StatusCode: StatusCodes.Status200OK);
    }

    private static bool IsStudentUser(
        AppUser user,
        ISet<string> studentObjectIds,
        ISet<string> studentEmails)
    {
        var hasStudentRole = user.UserRoles.Any(role =>
            role.Role.Name.Contains("student", StringComparison.OrdinalIgnoreCase));

        return hasStudentRole ||
            studentObjectIds.Contains(user.EntraObjectId) ||
            (!string.IsNullOrWhiteSpace(user.Email) && studentEmails.Contains(user.Email));
    }

    private static bool IsLecturerUser(AppUser user)
    {
        return user.UserRoles.Any(role =>
            string.Equals(role.Role.Name, LmsRoles.Lecturer, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<GetManagedUserResult> GetManagedUserAsync(string entraObjectId, CancellationToken ct = default)
    {
        var user = await userRepository.GetManagementProfileByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            return new GetManagedUserResult(false, ErrorCode: "user_not_found", ErrorMessage: "User was not found.", StatusCode: StatusCodes.Status404NotFound);
        }

        Guid? deptId = user.DepartmentId;
        Guid? facId = user.FacultyId;
        string? deptName = user.Department?.Name;
        string? facName = user.Faculty?.Name;
        string? academicProgramName = null;
        string? levelName = null;
        Guid? studentId = null;

        if (user.UserRoles.Any(r => r.Role.Name.Contains("student", StringComparison.OrdinalIgnoreCase)))
        {
            var student = await dbContext.Students
                .AsNoTracking()
                .Include(s => s.AcademicProgram)
                    .ThenInclude(p => p!.Department)
                        .ThenInclude(d => d!.Faculty)
                .Include(s => s.Faculty)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.EntraObjectId == entraObjectId || (!string.IsNullOrEmpty(user.Email) && s.OfficialEmail == user.Email), ct);
                
            if (student != null)
            {
                deptId = student.AcademicProgram?.DepartmentId ?? student.AcademicProgramId; // Fallback if no department
                facId = student.FacultyId ?? student.AcademicProgram?.Department?.FacultyId;
                deptName = student.AcademicProgram?.Department?.Name;
                facName = student.Faculty?.Name ?? student.AcademicProgram?.Department?.Faculty?.Name;
                academicProgramName = student.AcademicProgram?.Name;
                levelName = student.Level?.Name;
                studentId = student.Id;
            }
        }

        var now = DateTime.UtcNow;
        var effectivePermissions = await permissionService.GetEffectivePermissionsAsync(user.Id, ct);
        var detail = new ManagedUserDetail(
            user.EntraObjectId,
            user.Email,
            user.DisplayName,
            user.Username,
            user.IsActive,
            user.CreatedUtc,
            user.UpdatedUtc,
            user.UserRoles
                .Select(x => x.Role.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            user.UserPermissions
                .OrderBy(x => x.Permission.Code, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ManagedUserPermissionOverride(
                    x.Permission.Code,
                    x.Effect,
                    x.Reason,
                    x.ModifiedUtc,
                    x.ExpiresUtc,
                    !x.ExpiresUtc.HasValue || x.ExpiresUtc > now))
                .ToArray(),
            effectivePermissions
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            deptId,
            facId,
            academicProgramName,
            levelName,
            studentId,
            deptName,
            facName);

        return new GetManagedUserResult(true, detail, StatusCode: StatusCodes.Status200OK);
    }

    public async Task<SetManagedUserStatusResult> SetManagedUserStatusAsync(string entraObjectId, bool isActive, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            return new SetManagedUserStatusResult(false, ErrorCode: "user_not_found", ErrorMessage: "User was not found.", StatusCode: StatusCodes.Status404NotFound);
        }

        user.IsActive = isActive;
        user.UpdatedUtc = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);

        return new SetManagedUserStatusResult(true, user.EntraObjectId, user.IsActive, StatusCode: StatusCodes.Status200OK);
    }

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

        if (string.Equals(role.Name, LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            && !await IsCurrentActorSuperAdminAsync(ct))
        {
            return new AssignUserRoleResult(false, ErrorCode: "super_admin_required", ErrorMessage: "Only a SuperAdmin can assign the SuperAdmin role.", StatusCode: StatusCodes.Status403Forbidden);
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

        if (string.Equals(role.Name, LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            && !await IsCurrentActorSuperAdminAsync(ct))
        {
            return new RevokeUserRoleResult(false, ErrorCode: "super_admin_required", ErrorMessage: "Only a SuperAdmin can revoke the SuperAdmin role.", StatusCode: StatusCodes.Status403Forbidden);
        }

        var removed = await userRoleRepository.RevokeAsync(user.Id, role.Id, ct);
        if (removed)
        {
            await userRoleRepository.SaveChangesAsync(ct);
        }

        return new RevokeUserRoleResult(true, user.EntraObjectId, role.Name, StatusCode: StatusCodes.Status200OK);
    }

    public async Task<SetUserPermissionResult> SetUserPermissionAsync(string entraObjectId, string permissionCode, PermissionEffect effect, string? reason, DateTime? expiresUtc, CancellationToken ct = default)
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

        if (string.Equals(permission.Code, LmsPermissions.AccessManage, StringComparison.OrdinalIgnoreCase)
            && !await IsCurrentActorSuperAdminAsync(ct))
        {
            return new SetUserPermissionResult(false, ErrorCode: "super_admin_required", ErrorMessage: "Only a SuperAdmin can grant or revoke access management.", StatusCode: StatusCodes.Status403Forbidden);
        }

        if (expiresUtc.HasValue && expiresUtc.Value <= DateTime.UtcNow)
        {
            return new SetUserPermissionResult(false, ErrorCode: "invalid_expiry", ErrorMessage: "Expiry time must be in the future.", StatusCode: StatusCodes.Status400BadRequest);
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
        record.ExpiresUtc = expiresUtc;
        await userPermissionRepository.SaveChangesAsync(ct);

        return new SetUserPermissionResult(
            true,
            EntraObjectId: user.EntraObjectId,
            PermissionCode: permission.Code,
            Effect: effect,
            Reason: reason,
            ExpiresUtc: expiresUtc,
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
        var sorted = permissions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return new GetEffectivePermissionsResult(true, user.EntraObjectId, sorted, StatusCode: StatusCodes.Status200OK);
    }

    private async Task<bool> IsCurrentActorSuperAdminAsync(CancellationToken ct)
    {
        var actorUserId = await currentUserContext.GetUserIdAsync(ct);
        if (!actorUserId.HasValue)
        {
            return false;
        }

        var actorPermissions = await permissionService.GetEffectivePermissionsAsync(actorUserId.Value, ct);
        return actorPermissions.Contains(LmsPermissions.AccessManage)
            && (await userRoleRepository.GetRoleNamesAsync(actorUserId.Value, ct))
                .Contains(LmsRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
    }
}
