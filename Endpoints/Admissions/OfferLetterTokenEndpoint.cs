using FastEndpoints;
using LMS.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed class OfferLetterTokenRequest
{
    public Guid Id { get; set; }
}

public sealed class OfferLetterTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
}

public sealed class OfferLetterTokenEndpoint(
    IAdmissionService admissionService,
    IOptions<LMS.Api.Security.JwtSettings> jwtOptions)
    : Endpoint<OfferLetterTokenRequest, OfferLetterTokenResponse>
{
    private const int TokenLifetimeMinutes = 15;

    public override void Configure()
    {
        Post("admissions/applications/{Id}/offer-letter-token");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Issue Offer Letter Download Token")
            .WithTags("Admissions")
            .WithSummary("Issue a short-lived JWT bound to a specific application id, used to download the offer letter PDF."));
    }

    public override async Task HandleAsync(OfferLetterTokenRequest req, CancellationToken ct)
    {
        var app = await admissionService.GetApplicationByIdAsync(req.Id);
        if (app == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var validStatuses = new[] { "Admitted", "OfferAccepted", "FeePaid" };
        if (!validStatuses.Contains(app.Status.ToString()))
        {
            await Send.ResultAsync(Microsoft.AspNetCore.Http.TypedResults.Json(
                new { success = false, statusCode = 400, message = "Offer letter not available",
                      errors = new[] { new { code = "invalid_state",
                          message = $"Offer letter is only available for admitted applications. Current status: {app.Status}." } } },
                statusCode: 400));
            return;
        }

        var jwt = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, $"offer:{req.Id}"),
                new Claim("appId", req.Id.ToString()),
                new Claim("scope", "offer-letter-download")
            },
            notBefore: now,
            expires: now.AddMinutes(TokenLifetimeMinutes),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var response = new OfferLetterTokenResponse
        {
            Token = tokenString,
            ExpiresInMinutes = TokenLifetimeMinutes
        };
        await HttpContext.Response.WriteAsJsonAsync(response, ct);
    }
}