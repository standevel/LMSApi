using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Auth;

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordEndpoint(
    ILocalAuthService localAuthService,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<ChangePasswordRequest, AuthActionResult>
{
    public override void Configure()
    {
        Post("auth/change-password");
        Tags("Authentication");
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await localAuthService.ChangePasswordAsync(userId.Value, req.CurrentPassword, req.NewPassword, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to change password.",
                result.ErrorCode ?? "change_password_failed",
                result.ErrorMessage ?? "Unable to change password.",
                ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message ?? "Password updated successfully");
    }
}
