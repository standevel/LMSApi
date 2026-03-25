using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Endpoints;
using LMS.Api.Data.Entities;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin;

public sealed class SetUserPermissionRequest
{
    public string EntraObjectId { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public PermissionEffect Effect { get; set; }
    public string? Reason { get; set; }
}

public sealed record UserPermissionMutationResponse(string EntraObjectId, string PermissionCode, string Effect, string? Reason);

public sealed class SetUserPermissionEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpoint<SetUserPermissionRequest, UserPermissionMutationResponse>
{
    public override void Configure()
    {
        Post("/api/admin/users/permissions/set");
        Policies(LmsPolicies.Management);
    }

    public override async Task HandleAsync(SetUserPermissionRequest req, CancellationToken ct)
    {
        var result = await adminAuthzService.SetUserPermissionAsync(
            req.EntraObjectId,
            req.PermissionCode,
            req.Effect,
            req.Reason,
            ct);

        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.StatusCode == StatusCodes.Status404NotFound ? "User not found." : "Invalid permission",
                result.ErrorCode ?? "permission_update_failed",
                result.ErrorMessage ?? "Unable to update user permission override.",
                ct);
            return;
        }

        var data = new UserPermissionMutationResponse(
            result.EntraObjectId!,
            result.PermissionCode!,
            result.Effect.ToString(),
            result.Reason);

        await SendSuccessAsync(data, ct, "User permission override updated");
    }
}
