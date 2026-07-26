using FastEndpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Roles;

public sealed class ListRolesWithPermissionsEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpointWithoutRequest<List<RolePermissionSummary>>
{
    public override void Configure()
    {
        Get("admin/roles/permissions");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await adminAuthzService.ListRolesWithPermissionsAsync(ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                "Unable to retrieve roles.",
                result.ErrorCode ?? "roles_query_failed",
                result.ErrorMessage ?? "Unable to retrieve roles.",
                ct);
            return;
        }

        await SendSuccessAsync(result.Roles?.ToList() ?? [], ct);
    }
}
