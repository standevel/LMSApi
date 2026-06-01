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
        Post("admissions/validate-transfer");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Validate Transfer Eligibility") 
            .WithTags("Admissions")
            .WithSummary("Validate eligibility for transfer student admission"));
    }

    public override async Task HandleAsync(ValidateTransferEligibilityRequest req, CancellationToken ct)
    {
        var result = await admissionService.ValidateTransferEligibilityAsync(req.ApplicationId);
        await SendSuccessAsync(result, ct);
    }
}
