using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetDocumentTypesRequest
{
    public string? ApplicantType { get; set; }
    public Guid? ProgramId { get; set; }
}

public sealed class GetDocumentTypesEndpoint(IDocumentService documentService, IAdmissionService admissionService)
    : ApiEndpoint<GetDocumentTypesRequest, IEnumerable<DocumentTypeResponse>>
{
    public override void Configure()
    {
        Get("admissions/document-types");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Get Document Types") 
            .WithTags("Admissions")
            .WithSummary("Retrieve active document types required for admission"));
    }

    public override async Task HandleAsync(GetDocumentTypesRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.ApplicantType) && Enum.TryParse<ApplicantType>(req.ApplicantType, out var applicantType))
        {
            var requiredTypes = await admissionService.GetRequiredDocumentTypesAsync(applicantType, req.ProgramId);
            var filteredResponse = requiredTypes.Select(t => new DocumentTypeResponse(
                t.Id, t.Name, t.Code, t.Category.ToString(), t.IsCompulsory,
                t.InternationalOnly, t.DirectEntryOnly, t.TransferOnly, t.NigeriaOnly
            ));
            await SendSuccessAsync(filteredResponse, ct);
            return;
        }

        var types = await documentService.GetActiveDocumentTypesAsync(DocumentCategory.Admission);

        var response = types.Select(t => new DocumentTypeResponse(
            t.Id, t.Name, t.Code, t.Category.ToString(), t.IsCompulsory,
            t.InternationalOnly, t.DirectEntryOnly, t.TransferOnly, t.NigeriaOnly
        ));

        await SendSuccessAsync(response, ct);
    }
}
