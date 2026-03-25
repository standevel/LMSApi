using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LMS.Api.Security;

public sealed class LocalAuthService(
    IUserRepository userRepository,
    IPasswordHasher<AppUser> passwordHasher,
    ITokenService tokenService,
    IOptions<JwtSettings> jwtOptions) : ILocalAuthService
{
    public async Task<LoginServiceResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginServiceResult(
                Success: false,
                ErrorCode: "invalid_login_request",
                ErrorMessage: "Username and password are required.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedUsername = username.Trim();
        var user = await userRepository.GetActiveByUsernameAsync(normalizedUsername, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new LoginServiceResult(
                Success: false,
                ErrorCode: "invalid_credentials",
                ErrorMessage: "Invalid username or password.",
                StatusCode: StatusCodes.Status401Unauthorized);
        }

        var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
        {
            return new LoginServiceResult(
                Success: false,
                ErrorCode: "invalid_credentials",
                ErrorMessage: "Invalid username or password.",
                StatusCode: StatusCodes.Status401Unauthorized);
        }

        var token = await tokenService.CreateAccessTokenAsync(user.Id, ct);
        var expiresIn = Math.Max(60, jwtOptions.Value.ExpiryMinutes * 60);
        return new LoginServiceResult(
            Success: true,
            AccessToken: token,
            ExpiresInSeconds: expiresIn,
            StatusCode: StatusCodes.Status200OK);
    }

    public async Task<SetCredentialsServiceResult> SetCredentialsAsync(string entraObjectId, string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entraObjectId) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new SetCredentialsServiceResult(
                Success: false,
                ErrorCode: "invalid_request",
                ErrorMessage: "EntraObjectId, username, and password are required.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByEntraObjectIdAsync(entraObjectId, ct);
        if (user is null)
        {
            return new SetCredentialsServiceResult(
                Success: false,
                ErrorCode: "user_not_found",
                ErrorMessage: "User was not found.",
                StatusCode: StatusCodes.Status404NotFound);
        }

        var normalizedUsername = username.Trim();
        var taken = await userRepository.UsernameExistsAsync(normalizedUsername, user.Id, ct);
        if (taken)
        {
            return new SetCredentialsServiceResult(
                Success: false,
                ErrorCode: "username_taken",
                ErrorMessage: $"Username '{normalizedUsername}' is already in use.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        user.Username = normalizedUsername;
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.UpdatedUtc = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);

        return new SetCredentialsServiceResult(
            Success: true,
            EntraObjectId: user.EntraObjectId,
            Username: normalizedUsername,
            StatusCode: StatusCodes.Status200OK);
    }
}
