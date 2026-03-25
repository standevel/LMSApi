using LMS.Api.Data.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LMS.Api.Security;

public sealed class JwtTokenService(
    IUserRepository userRepository,
    IUserRoleRepository userRoleRepository,
    IOptions<JwtSettings> options) : ITokenService
{
    public async Task<string> CreateAccessTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var roles = await userRoleRepository.GetRoleNamesAsync(userId, ct);

        var jwt = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("oid", user.EntraObjectId),
            new("name", user.DisplayName ?? user.Username ?? user.Email ?? user.Id.ToString()),
            new("preferred_username", user.Username ?? user.Email ?? user.EntraObjectId)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("roles", role));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(jwt.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
