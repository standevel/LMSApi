using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class RespondToOfferRequest
{
    public Guid Id { get; set; }
    public bool AcceptOffer { get; set; }
}

public sealed class RespondToOfferEndpoint(IAdmissionService admissionService, LmsDbContext dbContext)
    : ApiEndpoint<RespondToOfferRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Post("/api/admissions/offers/{Id}/decision");
        AllowAnonymous();
    }
    
    public override async Task HandleAsync(RespondToOfferRequest req, CancellationToken ct)
    {
        try
        {
            var app = await admissionService.RespondToOfferAsync(req.Id, req.AcceptOffer);
            var (studentUserId, feeRecord) = await AdmissionResponseMapper.GetOfferFeeContextAsync(dbContext, app, ct);
            var response = AdmissionResponseMapper.Map(app, studentUserId, feeRecord);

            await SendSuccessAsync(response, ct, req.AcceptOffer ? "Offer accepted successfully." : "Offer rejected successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Application not found", "not_found", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Invalid offer state", "invalid_offer_state", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Error processing offer response", "processing_error", $"An error occurred while processing your offer response: {ex.Message}", ct);
        }
    }
}
