using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class ValidateTransferEligibilityRequest
{
    public Guid ApplicationId { get; set; }
}

public sealed class ValidateTransferEligibilityEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<ValidateTransferEligibilityRequest, TransferValidationResult>
{
    public override void Configure()
    {
        Post("/api/admissions/validate-transfer");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ValidateTransferEligibilityRequest req, CancellationToken ct)
    {
        var result = await admissionService.ValidateTransferEligibilityAsync(req.ApplicationId);
        await SendSuccessAsync(result, ct);
    }
}
