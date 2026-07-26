using FastEndpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Roles;

public sealed record UpdateRolePermissionsRequest(string RoleName, List<string> PermissionCodes);

public sealed class UpdateRolePermissionsEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpoint<UpdateRolePermissionsRequest, UpdateRolePermissionsResult>
{
    public override void Configure()
    {
        Put("admin/roles/{RoleName}/permissions");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
        Tags("Administration");
    }

    public override async Task HandleAsync(UpdateRolePermissionsRequest req, CancellationToken ct)
    {
        var roleName = Route<string>("RoleName");
        var result = await adminAuthzService.UpdateRolePermissionsAsync(roleName ?? req.RoleName, req.PermissionCodes, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to update role permissions.",
                result.ErrorCode ?? "update_role_permissions_failed",
                result.ErrorMessage ?? "Unable to update role permissions.",
                ct);
            return;
        }

        await SendSuccessAsync(result, ct);
    }
}
