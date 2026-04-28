using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Endpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin;

public sealed class AssignUserRoleRequest
{
    public string EntraObjectId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}

public sealed record UserRoleMutationResponse(string EntraObjectId, string RoleName, string Action);

public sealed class AssignUserRoleEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpoint<AssignUserRoleRequest, UserRoleMutationResponse>
{
    public override void Configure()
    {
        Post("/api/admin/users/roles/assign");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
    }

    public override async Task HandleAsync(AssignUserRoleRequest req, CancellationToken ct)
    {
        var result = await adminAuthzService.AssignUserRoleAsync(req.EntraObjectId, req.RoleName, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.StatusCode == StatusCodes.Status404NotFound ? "User not found." : "Invalid role",
                result.ErrorCode ?? "role_assignment_failed",
                result.ErrorMessage ?? "Role assignment failed.",
                ct);
            return;
        }

        var data = new UserRoleMutationResponse(result.EntraObjectId!, result.RoleName!, "assigned");
        await SendSuccessAsync(data, ct, "Role assignment updated");
    }
}
