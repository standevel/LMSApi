using FastEndpoints;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed class DownloadDocumentRequest
{
    public Guid Id { get; set; }
}

public sealed class DownloadDocumentEndpoint(
    IDocumentService documentService,
    IFileStorageService fileStorageService,
    ICurrentUserContext currentUserContext)
    : Endpoint<DownloadDocumentRequest>
{
    public override void Configure()
    {
        Get("documents/download/{Id}");
        Roles("SuperAdmin", "Admin", "Registrar", "Lecturer", "Student", "Parent");
        Tags("Admissions");
        Description(d => d
            .WithName("Download Document") 
            .WithTags("Admissions")
            .WithSummary("Download a previously uploaded admission document file"));
    }

    public override async Task HandleAsync(DownloadDocumentRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
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

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
