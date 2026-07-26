using FastEndpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Roles;

public sealed record CreateCustomRoleRequest(string RoleName, string? Description);

public sealed class CreateCustomRoleEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpoint<CreateCustomRoleRequest, RolePermissionSummary>
{
    public override void Configure()
    {
        Post("admin/roles");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
        Tags("Administration");
    }

    public override async Task HandleAsync(CreateCustomRoleRequest req, CancellationToken ct)
    {
        var result = await adminAuthzService.CreateCustomRoleAsync(req.RoleName, req.Description, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to create role.",
                result.ErrorCode ?? "create_role_failed",
                result.ErrorMessage ?? "Unable to create role.",
                ct);
            return;
        }

        await SendSuccessAsync(result.Role!, ct);
    }
}
