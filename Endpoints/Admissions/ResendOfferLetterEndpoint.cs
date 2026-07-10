using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class ResendOfferLetterEndpoint(IAdmissionService admissionService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Post("admissions/applications/{id}/resend-offer");
        Policies(LmsPolicies.AdmissionsManagement);
        Tags("Admissions");
        Description(d => d
            .WithName("Resend Offer Letter")
            .WithSummary("Regenerate and resend the admission offer letter PDF to the applicant's email. Refreshes offer expiry by 14 days."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            var app = await admissionService.ResendOfferLetterAsync(id, userId, ct);
            var response = AdmissionResponseMapper.Map(app);
            await SendSuccessAsync(response, ct, "Offer letter resent successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Application not found", "not_found", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Cannot resend offer", "invalid_state", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Failed to resend offer letter", "resend_error", ex.Message, ct);
        }
    }
}
