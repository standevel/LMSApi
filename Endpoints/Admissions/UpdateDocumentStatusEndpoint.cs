using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed class UpdateDocumentStatusEndpoint(IDocumentService documentService)
    : ApiEndpoint<UpdateDocumentStatusRequest, DocumentResponse>
{
    public override void Configure()
    {
        Patch("/api/admissions/documents/{Id}/status");
        AllowAnonymous(); // TODO: Restrict to Registry/Admin roles
    }

    public override async Task HandleAsync(UpdateDocumentStatusRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("Id");
        
        if (!Enum.TryParse<DocumentStatus>(req.Status, true, out var status))
        {
            await SendFailureAsync(400, "Invalid Status", "invalid_status", $"Status '{req.Status}' is not valid.", ct);
            return;
        }

        try
        {
            var doc = await documentService.UpdateDocumentStatusAsync(id, status, req.RejectionReason);

            var response = new DocumentResponse(
                doc.Id,
                doc.FileName,
                doc.FileUrl,
                doc.DocumentTypeId,
                doc.DocumentType?.Name ?? "Document",
                doc.DocumentType?.Code ?? string.Empty,
                doc.Status.ToString(),
                doc.RejectionReason
            );

            await SendSuccessAsync(response, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Document Not Found", "not_found", ex.Message, ct);
        }
    }
}
