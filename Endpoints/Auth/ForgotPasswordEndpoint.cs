using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Auth;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string? ResetBaseUrl { get; set; }
}

public sealed class ForgotPasswordEndpoint(ILocalAuthService localAuthService)
    : ApiEndpoint<ForgotPasswordRequest, AuthActionResult>
{
    public override void Configure()
    {
        Post("auth/forgot-password");
        AllowAnonymous();
        Tags("Authentication");
    }

    public override async Task HandleAsync(ForgotPasswordRequest req, CancellationToken ct)
    {
        var result = await localAuthService.ForgotPasswordAsync(req.Email, req.ResetBaseUrl, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.ErrorMessage ?? "Unable to process password reset request.",
                result.ErrorCode ?? "forgot_password_failed",
                result.ErrorMessage ?? "Unable to process password reset request.",
                ct);
            return;
        }

        await SendSuccessAsync(result, ct, result.Message ?? "Password reset instructions sent.");
    }
}
