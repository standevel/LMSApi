using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LMS.Api.Security;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;

public static class PermissionPolicy
{
    public const string Prefix = "permission:";

    public static string Build(string permissionCode) => $"{Prefix}{permissionCode}";
}

public sealed class PermissionAuthorizationHandler(
    ICurrentUserContext currentUserContext,
    IPermissionService permissionService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userId = await currentUserContext.GetUserIdAsync();
        if (!userId.HasValue)
        {
            return;
        }

        var hasPermission = await permissionService.HasPermissionAsync(userId.Value, requirement.PermissionCode);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var permissionCode = policyName[PermissionPolicy.Prefix.Length..];
            var builder = new AuthorizationPolicyBuilder();
            builder.RequireAuthenticatedUser();
            builder.AddRequirements(new PermissionRequirement(permissionCode));
            return builder.Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
