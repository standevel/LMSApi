using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admissions;

public sealed class ResubmitDocumentRequest
{
    public Guid ApplicationId { get; set; }
    public Guid NewDocumentId { get; set; }
}

public sealed class ResubmitDocumentEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<ResubmitDocumentRequest, DocumentResponse>
{
    public override void Configure()
    {
        Post("/api/admissions/resubmit/{DocumentId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ResubmitDocumentRequest req, CancellationToken ct)
    {
        var oldDocumentId = Route<Guid>("DocumentId");

        var oldDoc = await dbContext.DocumentRecords
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == oldDocumentId, ct);

        if (oldDoc is null)
        {
            await SendFailureAsync(404, "Document not found", "not_found", "The original document was not found.", ct);
            return;
        }

        if (oldDoc.Status != DocumentStatus.Rejected)
        {
            await SendFailureAsync(400, "Document is not rejected", "invalid_state", "Only rejected documents can be resubmitted.", ct);
            return;
        }

        var application = await dbContext.AdmissionApplications
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == req.ApplicationId, ct);

        if (application is null)
        {
            await SendFailureAsync(404, "Application not found", "not_found", "The application was not found.", ct);
            return;
        }

        var oldDocInApp = application.Documents.FirstOrDefault<DocumentRecord>(d => d.Id == oldDocumentId);
        if (oldDocInApp is null)
        {
            await SendFailureAsync(400, "Document not linked", "invalid_state", "The document is not linked to this application.", ct);
            return;
        }

        var newDoc = await dbContext.DocumentRecords
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == req.NewDocumentId, ct);

        if (newDoc is null)
        {
            await SendFailureAsync(404, "New document not found", "not_found", "The replacement document was not found.", ct);
            return;
        }

        // Swap: remove old, add new
        application.Documents.Remove(oldDocInApp);
        application.Documents.Add(newDoc);
        application.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        var response = new DocumentResponse(
            newDoc.Id,
            newDoc.FileName,
            newDoc.FileUrl,
            newDoc.DocumentTypeId,
            newDoc.DocumentType?.Name ?? "Document",
            newDoc.DocumentType?.Code ?? string.Empty,
            newDoc.Status.ToString(),
            newDoc.RejectionReason
        );

        await SendSuccessAsync(response, ct);
    }
}
