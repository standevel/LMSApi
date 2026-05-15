using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetRequiredDocumentTypesRequest
{
    public string? ApplicantType { get; set; }
    public Guid? ProgramId { get; set; }
}

public sealed class GetRequiredDocumentTypesEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<GetRequiredDocumentTypesRequest, IEnumerable<DocumentTypeResponse>>
{
    public override void Configure()
    {
        Get("/api/admissions/required-document-types");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetRequiredDocumentTypesRequest req, CancellationToken ct)
    {
        // Parse applicant type from query string
        ApplicantType applicantType = ApplicantType.UTME;
        if (!string.IsNullOrEmpty(req.ApplicantType))
        {
            if (!Enum.TryParse<ApplicantType>(req.ApplicantType, out var parsed))
            {
                await SendFailureAsync(400, "Invalid applicant type", "validation_error", "ApplicantType is not valid.", ct);
                return;
            }
            applicantType = parsed;
        }

        var documentTypes = await admissionService.GetRequiredDocumentTypesAsync(applicantType, req.ProgramId);
        var response = documentTypes.Select(d => new DocumentTypeResponse(
            d.Id,
            d.Name,
            d.Code,
            d.Category.ToString(),
            d.IsCompulsory,
            d.InternationalOnly,
            d.DirectEntryOnly,
            d.TransferOnly,
            d.NigeriaOnly
        ));

        await SendSuccessAsync(response, ct);
    }
}
