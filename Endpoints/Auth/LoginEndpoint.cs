using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Endpoints;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Auth;

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresInSeconds);

public sealed class LoginEndpoint(ILocalAuthService localAuthService)
    : ApiEndpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var result = await localAuthService.LoginAsync(req.Username, req.Password, ct);
        if (!result.Success)
        {
            await SendFailureAsync(
                result.StatusCode,
                result.StatusCode == StatusCodes.Status401Unauthorized ? "Authentication failed." : "Invalid request.",
                result.ErrorCode ?? "login_failed",
                result.ErrorMessage ?? "Unable to login.",
                ct);
            return;
        }

        var data = new LoginResponse(result.AccessToken!, "Bearer", result.ExpiresInSeconds);
        await SendSuccessAsync(data, ct);
    }
}
