using LMS.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace LMS.Api.Security;

public sealed record PermissionRequirement(IReadOnlyList<string> PermissionCodes) : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode) : this([permissionCode]) { }
}

public static class PermissionPolicy
{
    public const string Prefix = "permission:";

    public static string Build(string permissionCode) => $"{Prefix}{permissionCode}";

    public static string BuildAny(params string[] permissionCodes) => $"{Prefix}{string.Join("|", permissionCodes)}";
}

public sealed class PermissionAuthorizationHandler(
    ICurrentUserContext currentUserContext,
    IPermissionService permissionService,
    LmsDbContext dbContext) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // 1. SuperAdmin / Admin bypass
        if (context.User.IsInRole(LmsRoles.SuperAdmin) || context.User.IsInRole(LmsRoles.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        // 2. Check effective permissions via user ID (includes DB RolePermissions and individual UserPermissions overrides)
        var userId = await currentUserContext.GetUserIdAsync();
        if (userId.HasValue)
        {
            var effective = await permissionService.GetEffectivePermissionsAsync(userId.Value);
            if (requirement.PermissionCodes.Any(code => effective.Contains(code)))
            {
                context.Succeed(requirement);
                return;
            }
        }

        // 3. Check role claims in context against RolePermissions in DB (allows dynamic Role Matrix assignment to match token role claims)
        var userRoleClaims = context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userRoleClaims.Count > 0)
        {
            var hasRolePermission = await (
                from r in dbContext.Roles.AsNoTracking()
                join rp in dbContext.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                join p in dbContext.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                where userRoleClaims.Contains(r.Name) && requirement.PermissionCodes.Contains(p.Code)
                select p.Id
            ).AnyAsync();

            if (hasRolePermission)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // 4. Default role-permission fallback (ensures critical operational roles work even if DB RolePermissions table is not seeded)
        var defaultRoleMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [LmsRoles.Registrar] = [LmsPermissions.RecordsManage, LmsPermissions.EnrollmentsManage, LmsPermissions.UsersManage, LmsPermissions.AdmissionsManage, LmsPermissions.TimetableManage, LmsPermissions.HostelsView, LmsPermissions.ReportsView, LmsPermissions.ProfileView, LmsPermissions.ResultsPublish],
            [LmsRoles.AdmissionOfficer] = [LmsPermissions.AdmissionsManage, LmsPermissions.RecordsManage, LmsPermissions.EnrollmentsManage, LmsPermissions.UsersManage, LmsPermissions.ReportsView, LmsPermissions.ProfileView],
            [LmsRoles.AcademicAdmin] = [LmsPermissions.CoursesManage, LmsPermissions.TimetableManage, LmsPermissions.EnrollmentsManage, LmsPermissions.RecordsManage, LmsPermissions.ReportsView, LmsPermissions.ProfileView, LmsPermissions.ResultsPublish],
            [LmsRoles.Dean] = [LmsPermissions.CoursesManage, LmsPermissions.CoursesTeach, LmsPermissions.ReportsView, LmsPermissions.EnrollmentsManage, LmsPermissions.AdvisingManage, LmsPermissions.AdvisingAccess, LmsPermissions.ProfileView, LmsPermissions.ResultsPublish],
            [LmsRoles.HOD] = [LmsPermissions.CoursesManage, LmsPermissions.CoursesTeach, LmsPermissions.ReportsView, LmsPermissions.EnrollmentsManage, LmsPermissions.AdvisingManage, LmsPermissions.AdvisingAccess, LmsPermissions.ProfileView],
            [LmsRoles.ViceChancellor] = [LmsPermissions.UsersManage, LmsPermissions.RolesManage, LmsPermissions.PermissionsManage, LmsPermissions.CoursesManage, LmsPermissions.ReportsView, LmsPermissions.HostelsView, LmsPermissions.AdmissionsManage, LmsPermissions.FeesManage]
        };

        foreach (var role in userRoleClaims)
        {
            if (defaultRoleMap.TryGetValue(role, out var perms) &&
                requirement.PermissionCodes.Any(code => perms.Contains(code, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
                return;
            }
        }

        foreach (var role in defaultRoleMap.Keys)
        {
            if (context.User.IsInRole(role) &&
                requirement.PermissionCodes.Any(code => defaultRoleMap[role].Contains(code, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rawCodes = policyName[PermissionPolicy.Prefix.Length..];
            var codes = rawCodes.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var builder = new AuthorizationPolicyBuilder();
            builder.RequireAuthenticatedUser();
            builder.AddRequirements(new PermissionRequirement(codes));
            return builder.Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
