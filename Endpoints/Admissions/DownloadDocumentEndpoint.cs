using FastEndpoints;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed class DownloadDocumentRequest
{
    public Guid Id { get; set; }
}

public sealed class DownloadDocumentEndpoint(IDocumentService documentService, IFileStorageService fileStorageService)
    : Endpoint<DownloadDocumentRequest>
{
    public override void Configure()
    {
        Get("/api/documents/download/{Id}");
        AllowAnonymous(); // We can add permission checks in the handler
    }

    public override async Task HandleAsync(DownloadDocumentRequest req, CancellationToken ct)
    {
        var record = await documentService.GetDocumentByIdAsync(req.Id);

        if (record == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // TODO: Implement ValidateAccessAsync check here once auth is fully integrated
        // var hasAccess = await documentService.ValidateAccessAsync(record.Id, currentUser.Id, currentUser.Roles);
        // if (!hasAccess) { await Send.ForbiddenAsync(ct); return; }

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
