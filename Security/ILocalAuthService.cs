namespace LMS.Api.Security;

public interface ILocalAuthService
{
    Task<LoginServiceResult> LoginAsync(string username, string password, CancellationToken ct = default);
    Task<SetCredentialsServiceResult> SetCredentialsAsync(string entraObjectId, string username, string password, CancellationToken ct = default);
}

public sealed record LoginServiceResult(
    bool Success,
    string? AccessToken = null,
    int ExpiresInSeconds = 0,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);

public sealed record SetCredentialsServiceResult(
    bool Success,
    string? EntraObjectId = null,
    string? Username = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status400BadRequest);
