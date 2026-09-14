using FastEndpoints;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed class DownloadOfferLetterRequest
{
    public Guid Id { get; set; }

    /// <summary>
    /// Which document to download. Defaults to the offer letter.
    /// Supported values: "offer" (Admission_Letter.pdf) or "memo" (Advance_Payment_Memo.pdf).
    /// </summary>
    public string? Type { get; set; }
}

public sealed class DownloadOfferLetterEndpoint(
    IAdmissionService admissionService,
    ICurrentUserContext currentUserContext,
    IPermissionService permissionService,
    IOptions<LMS.Api.Security.JwtSettings> jwtOptions,
    ILogger<DownloadOfferLetterEndpoint> logger)
    : Endpoint<DownloadOfferLetterRequest>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override void Configure()
    {
        Get("admissions/applications/{Id}/offer-letter");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Download Offer Letter")
            .WithTags("Admissions")
            .WithSummary("Stream the generated admission offer letter (or advance payment memo) as a PDF for download or share."));
    }

    public override async Task HandleAsync(DownloadOfferLetterRequest req, CancellationToken ct)
    {
        var app = await admissionService.GetApplicationByIdAsync(req.Id);
        if (app == null)
        {
            await WriteFailureAsync(404, "Application not found", "not_found", "No application found with the given ID.", ct);
            return;
        }

        var validStatuses = new[] { "Admitted", "OfferAccepted", "FeePaid" };
        if (!validStatuses.Contains(app.Status.ToString()))
        {
            await WriteFailureAsync(400, "Offer letter not available", "invalid_state",
                $"Offer letter is only available for admitted applications. Current status: {app.Status}.", ct);
            return;
        }

        // Authorize: either an authenticated staff user, or an applicant presenting a short-lived
        // JWT bound to this specific application id (issued by OfferLetterTokenEndpoint).
        var authorized = await AuthorizeAsync(req, ct);
        if (!authorized)
        {
            await WriteFailureAsync(401, "Unauthorized", "unauthorized", "A valid offer letter download token is required.", ct);
            return;
        }

        var docType = (req.Type ?? "offer").ToLowerInvariant();
        byte[] pdf;
        string fileName;

        var applicantName = $"{app.FirstName}_{app.LastName}".Trim();
        var cleanApplicantName = string.Join("_", applicantName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");

        try
        {
            if (docType == "memo")
            {
                pdf = await admissionService.GenerateAdvancePaymentMemoPdfAsync(req.Id, ct);
                fileName = string.IsNullOrWhiteSpace(cleanApplicantName)
                    ? "Advance_Payment_Memo.pdf"
                    : $"{cleanApplicantName}_Advance_Payment_Memo.pdf";
            }
            else
            {
                pdf = await admissionService.GenerateOfferLetterPdfAsync(req.Id, ct);
                fileName = string.IsNullOrWhiteSpace(cleanApplicantName)
                    ? "Admission_Letter.pdf"
                    : $"{cleanApplicantName}_Admission_Offer_Letter.pdf";
            }
        }
        catch (KeyNotFoundException ex)
        {
            await WriteFailureAsync(404, "Application not found", "not_found", ex.Message, ct);
            return;
        }
        catch (InvalidOperationException ex)
        {
            await WriteFailureAsync(400, "Cannot generate offer letter", "invalid_state", ex.Message, ct);
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[OFFER-LETTER-DOWNLOAD-ERROR] Failed to generate PDF for application {ApplicationId}", req.Id);
            await WriteFailureAsync(500, "Failed to generate offer letter", "generation_error",
                "An error occurred while generating the offer letter PDF.", ct);
            return;
        }

        HttpContext.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        HttpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        HttpContext.Response.ContentType = "application/pdf";
        await HttpContext.Response.Body.WriteAsync(pdf, ct);
    }

    private async Task WriteFailureAsync(int statusCode, string message, string errorCode, string errorMessage, CancellationToken ct)
    {
        HttpContext.Response.StatusCode = statusCode;
        HttpContext.Response.ContentType = "application/json";
        var payload = new
        {
            success = false,
            statusCode,
            message,
            errors = new[] { new { code = errorCode, message = errorMessage } }
        };
        await HttpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), ct);
    }

    private async Task<bool> AuthorizeAsync(DownloadOfferLetterRequest req, CancellationToken ct)
    {
        var authUserId = await currentUserContext.GetUserIdAsync(ct);
        if (authUserId.HasValue)
        {
            // Authenticated staff - allow Admin, SuperAdmin, Registrar, AdmissionOfficer,
            // or any user with admissions management permissions/roles.
            var hasAllowedRole = User.Claims.Any(c =>
                (c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles") &&
                (c.Value.Equals(LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
                 c.Value.Equals(LmsRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
                 c.Value.Equals(LmsRoles.Registrar, StringComparison.OrdinalIgnoreCase) ||
                 c.Value.Equals(LmsRoles.AdmissionOfficer, StringComparison.OrdinalIgnoreCase) ||
                 c.Value.Equals(LmsPolicies.AdmissionsManagement, StringComparison.OrdinalIgnoreCase) ||
                 c.Value.Equals(LmsPermissions.AdmissionsManage, StringComparison.OrdinalIgnoreCase)));

            if (hasAllowedRole || await permissionService.HasPermissionAsync(authUserId.Value, LmsPermissions.AdmissionsManage, ct))
            {
                return true;
            }
        }

        // Anonymous applicant flow: validate a short-lived JWT bound to this specific application id.
        var token = HttpContext.Request.Query["token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Value.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Value.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            var appIdClaim = principal.FindFirstValue("appId") ?? principal.FindFirstValue("application_id");
            if (string.IsNullOrEmpty(appIdClaim) || !Guid.TryParse(appIdClaim, out var claimedAppId))
            {
                return false;
            }

            return claimedAppId == req.Id;
        }
        catch
        {
            return false;
        }
    }
}