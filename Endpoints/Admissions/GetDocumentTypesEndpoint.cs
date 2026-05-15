using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetDocumentTypesEndpoint(IDocumentService documentService)
    : ApiEndpoint<EmptyRequest, IEnumerable<DocumentTypeResponse>>
{
    public override void Configure()
    {
        Get("/api/admissions/document-types");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var types = await documentService.GetActiveDocumentTypesAsync(DocumentCategory.Admission);

        var response = types.Select(t => new DocumentTypeResponse(
            t.Id, t.Name, t.Code, t.Category.ToString(), t.IsCompulsory,
            t.InternationalOnly, t.DirectEntryOnly, t.TransferOnly, t.NigeriaOnly
        ));

        await SendSuccessAsync(response, ct);
    }
}
