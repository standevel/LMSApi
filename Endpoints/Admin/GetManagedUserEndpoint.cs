using LMS.Api.Endpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin;

public sealed record ManagedUserPermissionOverrideResponse(
    string PermissionCode,
    string Effect,
    string? Reason,
    DateTime ModifiedUtc,
    DateTime? ExpiresUtc,
    bool IsActive);

public sealed record ManagedUserDetailResponse(
    string EntraObjectId,
    string? Email,
    string? DisplayName,
    string? Username,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<ManagedUserPermissionOverrideResponse> PermissionOverrides,
    IReadOnlyCollection<string> EffectivePermissions);

public sealed class GetManagedUserEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpointWithoutRequest<ManagedUserDetailResponse>
{
    public override void Configure()
    {
        Get("/api/admin/users/{entraObjectId}");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var entraObjectId = Route<string>("entraObjectId");
        var result = await adminAuthzService.GetManagedUserAsync(entraObjectId ?? string.Empty, ct);
        if (!result.Success || result.User is null)
        {
            await SendFailureAsync(
                result.StatusCode,
                "User not found.",
                result.ErrorCode ?? "user_query_failed",
                result.ErrorMessage ?? "Unable to retrieve managed user.",
                ct);
            return;
        }

        var data = new ManagedUserDetailResponse(
            result.User.EntraObjectId,
            result.User.Email,
            result.User.DisplayName,
            result.User.Username,
            result.User.IsActive,
            result.User.CreatedUtc,
            result.User.UpdatedUtc,
            result.User.Roles,
            result.User.PermissionOverrides
                .Select(x => new ManagedUserPermissionOverrideResponse(
                    x.PermissionCode,
                    x.Effect.ToString(),
                    x.Reason,
                    x.ModifiedUtc,
                    x.ExpiresUtc,
                    x.IsActive))
                .ToArray(),
            result.User.EffectivePermissions);

        await SendSuccessAsync(data, ct);
    }
}
