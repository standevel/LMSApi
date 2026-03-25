using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Admissions;

public sealed class UploadDocumentRequest
{
    public Guid DocumentTypeId { get; set; }
    public Guid? OwnerId { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public List<IFormFile> Files { get; set; } = new();
}

public sealed class UploadDocumentEndpoint(IDocumentService documentService, IFileStorageService fileStorageService)
    : ApiEndpoint<UploadDocumentRequest, UploadMultipleDocumentsResponse>
{
    public override void Configure()
    {
        Post("/api/admissions/upload");
        AllowFileUploads();
        AllowAnonymous();
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
