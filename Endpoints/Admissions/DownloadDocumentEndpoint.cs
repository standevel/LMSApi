using FastEndpoints;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;

namespace LMS.Api.Endpoints.Admissions;

public sealed class DownloadDocumentRequest
{
    public Guid Id { get; set; }
}

public sealed class DownloadDocumentEndpoint(
    IDocumentService documentService,
    IFileStorageService fileStorageService,
    ICurrentUserContext currentUserContext,
    IUserRepository userRepository,
    IOptions<LMS.Api.Security.JwtSettings> jwtOptions)
    : Endpoint<DownloadDocumentRequest>
{
    public override void Configure()
    {
        Get("documents/download/{Id}");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Download Document") 
            .WithTags("Admissions")
            .WithSummary("Download a previously uploaded admission document file"));
    }

    public override async Task HandleAsync(DownloadDocumentRequest req, CancellationToken ct)
    {
        Guid? userId = null;
        var roles = new List<string>();

        var authUserId = await currentUserContext.GetUserIdAsync(ct);
        if (authUserId.HasValue)
        {
            userId = authUserId.Value;
            roles = User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            var token = HttpContext.Request.Query["token"].ToString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var handler = new JwtSecurityTokenHandler();
                try
                {
                    var principal = handler.ValidateToken(token, new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Value.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Value.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    }, out _);

                    var subjectId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? principal.FindFirstValue("sub")
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!string.IsNullOrEmpty(subjectId) && Guid.TryParse(subjectId, out var parsedUserId))
                    {
                        var user = await userRepository.GetByIdAsync(parsedUserId, ct);
                        if (user is not null && user.IsActive)
                        {
                            userId = parsedUserId;
                            roles = principal.Claims
                                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
                                .Select(c => c.Value)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                        }
                    }
                }
                catch
                {
                    await Send.UnauthorizedAsync(ct);
                    return;
                }
            }
        }

        if (!userId.HasValue)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var record = await documentService.GetDocumentByIdAsync(req.Id);

        if (record == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var hasAccess = await documentService.ValidateAccessAsync(record.Id, userId.Value, roles);
        if (!hasAccess)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var physicalPath = await fileStorageService.GetPhysicalPathAsync(record.FileUrl);

        if (physicalPath == null || !File.Exists(physicalPath))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{record.FileName}\"");
        await Send.FileAsync(new FileInfo(physicalPath), contentType: record.FileType, cancellation: ct);
    }
}
