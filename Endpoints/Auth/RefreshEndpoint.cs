using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LMS.Api.Endpoints.Auth;

/// <summary>
/// Accepts an expired (or still-valid) local JWT and issues a fresh one.
/// The endpoint is anonymous so that the expired token can still reach it;
/// the token is validated manually (signature + claims), skipping the
/// lifetime check on purpose.
/// </summary>
public sealed class RefreshEndpoint(
    IUserRepository userRepository,
    ITokenService tokenService,
    IOptions<JwtSettings> jwtOptions)
    : ApiEndpointWithoutRequest<LoginResponse>
{
    public override void Configure()
    {
        Post("auth/refresh");
        AllowAnonymous();   // We validate the token manually below (lifetime skipped)
        Tags("Authentication");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // ── 1. Extract the raw Bearer token from the Authorization header ──
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var tokenString = authHeader["Bearer ".Length..].Trim();

        // ── 2. Validate signature & claims but IGNORE expiry ──────────────
        var settings = jwtOptions.Value;
        var handler = new JwtSecurityTokenHandler();

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(tokenString, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = settings.Issuer,
                ValidateAudience         = true,
                ValidAudience            = settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(settings.SigningKey)),

                // Allow expired tokens through — that's the whole point
                ValidateLifetime         = false,

                ClockSkew = TimeSpan.Zero,
            }, out _);
        }
        catch
        {
            // Signature invalid or token malformed — genuine auth failure
            await SendUnauthorizedAsync(ct);
            return;
        }

        // ── 3. Resolve the user from the validated claims ─────────────────
        var subjectId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(subjectId) || !Guid.TryParse(subjectId, out var userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        // ── 4. Issue a fresh token ─────────────────────────────────────────
        var newToken = await tokenService.CreateAccessTokenAsync(user.Id, ct);
        var expiresIn = Math.Max(60, settings.ExpiryMinutes * 60);

        await SendSuccessAsync(new LoginResponse(newToken, "Bearer", expiresIn), ct);
    }
}
