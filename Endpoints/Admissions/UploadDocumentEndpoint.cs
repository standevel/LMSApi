using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admissions;

public sealed class UploadDocumentRequest
{
    public Guid DocumentTypeId { get; set; }
    public Guid? OwnerId { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public List<IFormFile> Files { get; set; } = new();
}

public sealed class UploadDocumentEndpoint(IDocumentService documentService, IFileStorageService fileStorageService, LmsDbContext dbContext)
    : ApiEndpoint<UploadDocumentRequest, UploadMultipleDocumentsResponse>
{
    public override void Configure()
    {
        Post("admissions/upload");
        AllowFileUploads();
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Upload Document") 
            .WithTags("Admissions")
            .WithSummary("Upload one or more admission documents (e.g., transcripts, certificates)"));
    }

    public override async Task HandleAsync(UploadDocumentRequest req, CancellationToken ct)
    {
        var responses = new List<DocumentResponse>();

        // Get document type to determine folder category
        var allTypes = await documentService.GetActiveDocumentTypesAsync();
        var docType = allTypes.FirstOrDefault(t => t.Id == req.DocumentTypeId);
        var category = docType?.Category.ToString() ?? "General";

        // Ensure we have a reference ID for folder grouping
        var refId = string.IsNullOrWhiteSpace(req.ReferenceId)
            ? (req.OwnerId?.ToString() ?? "anonymous")
            : req.ReferenceId;

        foreach (var file in req.Files)
        {
            using var stream = file.OpenReadStream();
            var relativePath = await fileStorageService.SaveFileAsync(category, refId, file.FileName, stream);

            var record = await documentService.UploadDocumentAsync(
                req.OwnerId,
                req.DocumentTypeId,
                file.FileName,
                relativePath, // This is now the relative physical path
                file.Length,
                file.ContentType
            );

            // Link document to application if referenceId or OwnerId is an AdmissionApplication ID
            Guid? appIdToLink = null;
            if (Guid.TryParse(refId, out var parsedRefId))
            {
                appIdToLink = parsedRefId;
            }
            else if (req.OwnerId.HasValue && req.OwnerId.Value != Guid.Empty)
            {
                appIdToLink = req.OwnerId.Value;
            }

            if (appIdToLink.HasValue)
            {
                var app = await dbContext.AdmissionApplications
                    .Include(a => a.Documents)
                    .FirstOrDefaultAsync(a => a.Id == appIdToLink.Value, ct);

                if (app != null && !app.Documents.Any(d => d.Id == record.Id))
                {
                    app.Documents.Add(record);
                    app.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            responses.Add(new DocumentResponse(
                record.Id,
                record.FileName,
                record.FileUrl,
                record.DocumentTypeId,
                docType?.Name ?? "Admission Document",
                docType?.Code ?? string.Empty,
                record.Status.ToString(),
                record.RejectionReason
            ));
        }

        await SendSuccessAsync(new UploadMultipleDocumentsResponse(responses), ct);
    }
}
