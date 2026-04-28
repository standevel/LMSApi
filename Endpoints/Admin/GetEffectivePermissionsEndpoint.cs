using FastEndpoints;
using LMS.Api.Endpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin;

public sealed record EffectivePermissionsResponse(string EntraObjectId, IReadOnlyCollection<string> Permissions);

public sealed class GetEffectivePermissionsEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpointWithoutRequest<EffectivePermissionsResponse>
{
    public override void Configure()
    {
        Get("/api/admin/users/{entraObjectId}/permissions");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var entraObjectId = Route<string>("entraObjectId");
        var result = await adminAuthzService.GetEffectivePermissionsAsync(entraObjectId ?? string.Empty, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                "User not found.",
                result.ErrorCode ?? "permissions_query_failed",
                result.ErrorMessage ?? "Unable to retrieve effective permissions.",
                ct);
            return;
        }

        var data = new EffectivePermissionsResponse(result.EntraObjectId!, result.Permissions ?? []);
        await SendSuccessAsync(data, ct);
    }
}
