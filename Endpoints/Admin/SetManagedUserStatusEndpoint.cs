using LMS.Api.Endpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin;

public sealed class SetManagedUserStatusRequest
{
    public string EntraObjectId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed record ManagedUserStatusResponse(string EntraObjectId, bool IsActive);

public sealed class SetManagedUserStatusEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpoint<SetManagedUserStatusRequest, ManagedUserStatusResponse>
{
    public override void Configure()
    {
        Patch("/api/admin/users/status");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
    }

    public override async Task HandleAsync(SetManagedUserStatusRequest req, CancellationToken ct)
    {
        var result = await adminAuthzService.SetManagedUserStatusAsync(req.EntraObjectId, req.IsActive, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.StatusCode == StatusCodes.Status404NotFound ? "User not found." : "Unable to update user status.",
                result.ErrorCode ?? "user_status_update_failed",
                result.ErrorMessage ?? "Unable to update user status.",
                ct);
            return;
        }

        await SendSuccessAsync(
            new ManagedUserStatusResponse(result.EntraObjectId!, result.IsActive),
            ct,
            result.IsActive ? "User activated" : "User deactivated");
    }
}
