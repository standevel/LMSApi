using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Auth;

public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ResetPasswordEndpoint(ILocalAuthService localAuthService)
    : ApiEndpoint<ResetPasswordRequest, AuthActionResult>
{
    public override void Configure()
    {
        Post("auth/reset-password");
        AllowAnonymous();
        Tags("Authentication");
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var result = await localAuthService.ResetPasswordAsync(req.Email, req.Token, req.NewPassword, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to reset password.",
                result.ErrorCode ?? "reset_password_failed",
                result.ErrorMessage ?? "Unable to reset password.",
                ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message ?? "Password reset successfully.");
    }
}
