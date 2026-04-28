using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Endpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin;

public sealed class SetLocalCredentialsRequest
{
    public string EntraObjectId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed record LocalCredentialsResponse(string EntraObjectId, string Username);

public sealed class SetLocalCredentialsEndpoint(ILocalAuthService localAuthService)
    : ApiEndpoint<SetLocalCredentialsRequest, LocalCredentialsResponse>
{
    public override void Configure()
    {
        Post("/api/admin/users/local-credentials/set");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
    }

    public override async Task HandleAsync(SetLocalCredentialsRequest req, CancellationToken ct)
    {
        var result = await localAuthService.SetCredentialsAsync(req.EntraObjectId, req.Username, req.Password, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.StatusCode == StatusCodes.Status404NotFound ? "User not found." : "Invalid request.",
                result.ErrorCode ?? "credentials_update_failed",
                result.ErrorMessage ?? "Unable to update local credentials.",
                ct);
            return;
        }

        var data = new LocalCredentialsResponse(result.EntraObjectId!, result.Username!);
        await SendSuccessAsync(data, ct, "Local credentials updated");
    }
}
