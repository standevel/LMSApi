using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetDocumentResubmissionContextRequest
{
    public Guid Id { get; set; }
}

public sealed class GetDocumentResubmissionContextEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<GetDocumentResubmissionContextRequest, DocumentResubmissionContextResponse>
{
    public override void Configure()
    {
        Get("/api/admissions/documents/{Id}/resubmission-context");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetDocumentResubmissionContextRequest req, CancellationToken ct)
    {
        var doc = await dbContext.DocumentRecords
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == req.Id, ct);

        if (doc is null)
        {
            await SendFailureAsync(404, "Document not found", "not_found", "No document found with the given ID.", ct);
            return;
        }

        if (doc.Status != DocumentStatus.Rejected)
        {
            await SendFailureAsync(400, "Document is not rejected", "invalid_state", "Only rejected documents can be resubmitted.", ct);
            return;
        }

        // Resolve the application that owns this document via the join table
        var applicationId = await dbContext.AdmissionApplications
            .Where(a => a.Documents.Any(d => d.Id == req.Id))
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);

        if (applicationId is null)
        {
            await SendFailureAsync(404, "Application not found", "not_found", "No application is linked to this document.", ct);
            return;
        }

        var response = new DocumentResubmissionContextResponse(
            doc.Id,
            doc.FileName,
            doc.FileUrl,
            doc.DocumentType?.Name ?? "Document",
            doc.DocumentType?.Code ?? string.Empty,
            doc.DocumentTypeId,
            doc.RejectionReason ?? string.Empty,
            doc.UploadedAt,
            applicationId.Value
        );

        await SendSuccessAsync(response, ct);
    }
}
