using LMS.Api.Endpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin;

public sealed record ManagedUserSummaryResponse(
    string EntraObjectId,
    string? Email,
    string? DisplayName,
    string? Username,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    IReadOnlyCollection<string> Roles);

public sealed record ListManagedUsersResponse(IReadOnlyCollection<ManagedUserSummaryResponse> Users);

public sealed class ListUsersEndpoint(IAdminAuthzService adminAuthzService)
    : ApiEndpointWithoutRequest<ListManagedUsersResponse>
{
    public override void Configure()
    {
        Get("/api/admin/users");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var search = Query<string?>("search", isRequired: false);
        var result = await adminAuthzService.ListManagedUsersAsync(search, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                "Unable to retrieve users.",
                result.ErrorCode ?? "user_list_failed",
                result.ErrorMessage ?? "Unable to retrieve managed users.",
                ct);
            return;
        }

        var data = new ListManagedUsersResponse(
            (result.Users ?? [])
                .Select(x => new ManagedUserSummaryResponse(
                    x.EntraObjectId,
                    x.Email,
                    x.DisplayName,
                    x.Username,
                    x.IsActive,
                    x.CreatedUtc,
                    x.UpdatedUtc,
                    x.Roles))
                .ToArray());

        await SendSuccessAsync(data, ct);
    }
}
