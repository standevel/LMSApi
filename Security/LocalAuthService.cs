using System.Security.Cryptography;
using System.Text;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using LMS.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LMS.Api.Security;

public sealed class LocalAuthService(
    IUserRepository userRepository,
    IPasswordHasher<AppUser> passwordHasher,
    ITokenService tokenService,
    IEmailService emailService,
    IConfiguration configuration,
    IOptions<JwtSettings> jwtOptions,
    LmsDbContext? dbContext = null,
    ILogger<LocalAuthService>? logger = null) : ILocalAuthService
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
        Console.WriteLine("Attempting login for username: " + normalizedUsername);
        var user = await userRepository.GetActiveByUsernameAsync(normalizedUsername, ct);
        if (user is null)
        {
            return new LoginServiceResult(
                Success: false,
                ErrorCode: "invalid_credentials",
                ErrorMessage: "Invalid username or password.",
                StatusCode: StatusCodes.Status401Unauthorized);
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
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

        if (dbContext != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var guardiansToLink = await dbContext.ParentGuardians
                .Where(pg => pg.UserId == Guid.Empty && pg.Email.ToLower() == user.Email.ToLower())
                .ToListAsync(ct);
            if (guardiansToLink.Count > 0)
            {
                foreach (var g in guardiansToLink)
                {
                    g.UserId = user.Id;
                }
                await dbContext.SaveChangesAsync(ct);
            }
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

    public async Task<AuthActionResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return new AuthActionResult(
                Success: false,
                ErrorCode: "invalid_password",
                ErrorMessage: "New password must be at least 6 characters long.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            return new AuthActionResult(
                Success: false,
                ErrorCode: "user_not_found",
                ErrorMessage: "User not found or inactive.",
                StatusCode: StatusCodes.Status404NotFound);
        }

        // If user already has a password set, verify current password
        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                return new AuthActionResult(
                    Success: false,
                    ErrorCode: "current_password_required",
                    ErrorMessage: "Current password is required.",
                    StatusCode: StatusCodes.Status400BadRequest);
            }

            var verify = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (verify == PasswordVerificationResult.Failed)
            {
                return new AuthActionResult(
                    Success: false,
                    ErrorCode: "invalid_current_password",
                    ErrorMessage: "The current password provided is incorrect.",
                    StatusCode: StatusCodes.Status400BadRequest);
            }
        }

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.UpdatedUtc = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);

        return new AuthActionResult(
            Success: true,
            Message: "Password has been successfully updated.",
            StatusCode: StatusCodes.Status200OK);
    }

    public async Task<AuthActionResult> ForgotPasswordAsync(string email, string? resetBaseUrl = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return new AuthActionResult(
                Success: false,
                ErrorCode: "email_required",
                ErrorMessage: "Email address is required.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedEmail = email.Trim();
        logger?.LogInformation("[ForgotPassword] Initiating password reset request for {Email}", normalizedEmail);

        var user = await userRepository.GetByEmailOrUsernameAsync(normalizedEmail, ct);

        // If user is found, ensure Email property is filled if Username was an email
        if (user != null)
        {
            if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(user.Username) && user.Username.Contains('@'))
            {
                user.Email = user.Username.Trim();
                await userRepository.SaveChangesAsync(ct);
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                user.UpdatedUtc = DateTime.UtcNow;
                await userRepository.SaveChangesAsync(ct);
            }
        }

        // Self-healing: If user is not found in AppUsers, check ParentGuardians or Student Emergency Contacts
        if (user == null && dbContext != null)
        {
            var lowerEmail = normalizedEmail.ToLower();
            var guardian = await dbContext.ParentGuardians
                .FirstOrDefaultAsync(pg => pg.Email.ToLower().Trim() == lowerEmail, ct);

            if (guardian != null && guardian.UserId != Guid.Empty)
            {
                user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == guardian.UserId, ct);
                if (user != null && string.IsNullOrWhiteSpace(user.Email))
                {
                    user.Email = normalizedEmail;
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            string? parentName = guardian != null ? $"{guardian.FirstName} {guardian.LastName}".Trim() : null;

            if (guardian == null && user == null)
            {
                var studentWithGuardian = await dbContext.Students
                    .FirstOrDefaultAsync(s => s.EmergencyContactEmail != null && s.EmergencyContactEmail.ToLower().Trim() == lowerEmail, ct);
                if (studentWithGuardian != null)
                {
                    parentName = studentWithGuardian.EmergencyContactName;
                    guardian = new ParentGuardian
                    {
                        Id = Guid.NewGuid(),
                        FirstName = !string.IsNullOrWhiteSpace(studentWithGuardian.EmergencyContactName)
                            ? studentWithGuardian.EmergencyContactName.Split(' ')[0]
                            : "Parent",
                        LastName = !string.IsNullOrWhiteSpace(studentWithGuardian.EmergencyContactName) && studentWithGuardian.EmergencyContactName.Contains(' ')
                            ? studentWithGuardian.EmergencyContactName.Substring(studentWithGuardian.EmergencyContactName.IndexOf(' ') + 1)
                            : "Guardian",
                        Email = normalizedEmail,
                        PhoneNumber = studentWithGuardian.EmergencyContactPhone ?? "N/A",
                        DateAddedUtc = DateTime.UtcNow
                    };
                    dbContext.ParentGuardians.Add(guardian);
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            if (guardian != null && user == null)
            {
                var parentRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Parent", ct);
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    EntraObjectId = $"parent:{Guid.NewGuid()}",
                    Username = normalizedEmail,
                    Email = normalizedEmail,
                    DisplayName = !string.IsNullOrWhiteSpace(parentName) ? parentName : "Parent/Guardian",
                    PasswordHash = passwordHasher.HashPassword(null!, "Parent@" + Guid.NewGuid().ToString("N")[..6] + "!"),
                    IsActive = true,
                    CreatedUtc = DateTime.UtcNow,
                    UpdatedUtc = DateTime.UtcNow
                };
                dbContext.Users.Add(user);
                if (parentRole != null)
                {
                    dbContext.UserRoles.Add(new UserRole
                    {
                        UserId = user.Id,
                        RoleId = parentRole.Id,
                        AssignedUtc = DateTime.UtcNow
                    });
                }
                guardian.UserId = user.Id;
                await dbContext.SaveChangesAsync(ct);
            }
        }

        // Always return success message for security to prevent user enumeration if account doesn't exist anywhere
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            logger?.LogWarning("[ForgotPassword] No user or guardian found for {Email}", normalizedEmail);
            return new AuthActionResult(
                Success: true,
                Message: "If an account with this email exists, a password reset link has been sent.",
                StatusCode: StatusCodes.Status200OK);
        }

        // Ensure any unlinked ParentGuardian records for this email are linked to the user
        if (dbContext != null)
        {
            var unlinkedGuardians = await dbContext.ParentGuardians
                .Where(pg => pg.UserId == Guid.Empty && pg.Email.ToLower().Trim() == user.Email.ToLower().Trim())
                .ToListAsync(ct);
            if (unlinkedGuardians.Count > 0)
            {
                foreach (var g in unlinkedGuardians)
                {
                    g.UserId = user.Id;
                }
                await dbContext.SaveChangesAsync(ct);
            }
        }

        var token = GeneratePasswordResetToken(user);
        var clientBase = GetClientBaseUrl(resetBaseUrl);
        var resetLink = $"{clientBase}/auth/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";

        try
        {
            logger?.LogInformation("[ForgotPassword] Dispatching reset email via Brevo to {Email}", user.Email);
            await emailService.SendPasswordResetEmailAsync(
                user.Email,
                user.DisplayName ?? user.Username ?? user.Email,
                resetLink);
            logger?.LogInformation("[ForgotPassword] Reset email successfully dispatched to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[ForgotPassword] Email dispatch failed for {Email}: {Message}", user.Email, ex.Message);
            Console.WriteLine($"[ForgotPassword] Email dispatch failed: {ex.Message}");
            return new AuthActionResult(
                Success: false,
                ErrorCode: "email_dispatch_failed",
                ErrorMessage: $"Unable to deliver password reset email: {ex.Message}",
                StatusCode: StatusCodes.Status500InternalServerError);
        }

        return new AuthActionResult(
            Success: true,
            Message: "If an account with this email exists, a password reset link has been sent.",
            StatusCode: StatusCodes.Status200OK);
    }

    public async Task<AuthActionResult> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
        {
            return new AuthActionResult(
                Success: false,
                ErrorCode: "invalid_request",
                ErrorMessage: "Email, token, and new password are required.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        if (newPassword.Length < 6)
        {
            return new AuthActionResult(
                Success: false,
                ErrorCode: "weak_password",
                ErrorMessage: "New password must be at least 6 characters long.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        var normalizedEmail = email.Trim();
        var user = await userRepository.GetByEmailOrUsernameAsync(normalizedEmail, ct);
        if (user == null && dbContext != null)
        {
            var guardian = await dbContext.ParentGuardians
                .FirstOrDefaultAsync(pg => pg.Email.ToLower().Trim() == normalizedEmail.ToLower(), ct);
            if (guardian != null && guardian.UserId != Guid.Empty)
            {
                user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == guardian.UserId, ct);
            }
        }

        if (user == null)
        {
            return new AuthActionResult(
                Success: false,
                ErrorCode: "invalid_reset_request",
                ErrorMessage: "Invalid or expired password reset token.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(user.Username) && user.Username.Contains('@'))
        {
            user.Email = user.Username.Trim();
        }

        if (!ValidatePasswordResetToken(token, user))
        {
            return new AuthActionResult(
                Success: false,
                ErrorCode: "invalid_token",
                ErrorMessage: "The password reset link is invalid or has expired. Please request a new one.",
                StatusCode: StatusCodes.Status400BadRequest);
        }

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.IsActive = true;
        user.UpdatedUtc = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);

        // Also ensure any unlinked ParentGuardian records are connected
        if (dbContext != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var unlinked = await dbContext.ParentGuardians
                .Where(pg => pg.UserId == Guid.Empty && pg.Email.ToLower() == user.Email.ToLower())
                .ToListAsync(ct);
            if (unlinked.Count > 0)
            {
                foreach (var g in unlinked)
                {
                    g.UserId = user.Id;
                }
                await dbContext.SaveChangesAsync(ct);
            }
        }

        return new AuthActionResult(
            Success: true,
            Message: "Your password has been reset successfully. You may now log in.",
            StatusCode: StatusCodes.Status200OK);
    }

    private string GetSecretKey()
    {
        if (!string.IsNullOrWhiteSpace(jwtOptions.Value.SigningKey))
            return jwtOptions.Value.SigningKey;

        var configKey = configuration["Jwt:SigningKey"];
        if (!string.IsNullOrWhiteSpace(configKey))
            return configKey;

        return "WigweUniversitySecureAuthDefaultSecretKey2026";
    }

    private string GetClientBaseUrl(string? requestedBaseUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(requestedBaseUrl) &&
            !requestedBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) &&
            !requestedBaseUrl.Contains("YOUR_FRONTEND_DOMAIN", StringComparison.OrdinalIgnoreCase))
        {
            return requestedBaseUrl.TrimEnd('/');
        }

        var configUrl = configuration["ClientApp:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configUrl) &&
            !configUrl.Contains("YOUR_FRONTEND_DOMAIN", StringComparison.OrdinalIgnoreCase) &&
            !configUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return configUrl.TrimEnd('/');
        }

        return "https://portal.wigweuniversity.edu.ng";
    }

    private string GeneratePasswordResetToken(AppUser user)
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();
        var payload = $"{user.Id}:{user.Email}:{expiry}";
        var secret = GetSecretKey();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signaturePayload = $"{payload}:{user.PasswordHash ?? "initial"}";
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signaturePayload)));
        var fullToken = $"{payload}:{signature}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(fullToken)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private bool ValidatePasswordResetToken(string token, AppUser user)
    {
        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = raw.Split(':');
            if (parts.Length < 4) return false;

            var userIdStr = parts[0];
            var email = parts[1];
            if (!long.TryParse(parts[2], out var expiryUnix)) return false;
            var signature = parts[3];

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiryUnix) return false;
            if (!Guid.TryParse(userIdStr, out var userId) || userId != user.Id) return false;
            if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase)) return false;

            var secret = GetSecretKey();
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var signaturePayload = $"{userIdStr}:{email}:{expiryUnix}:{user.PasswordHash ?? "initial"}";
            var expectedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signaturePayload)));

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expectedSignature));
        }
        catch
        {
            return false;
        }
    }
}
