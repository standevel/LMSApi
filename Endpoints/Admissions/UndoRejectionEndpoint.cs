using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class UndoRejectionEndpoint(IAdmissionService admissionService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Post("admissions/applications/{id}/undo-rejection");
        Policies(LmsPolicies.AdmissionsManagement);
        Tags("Admissions");
        Description(d => d
            .WithName("Undo Rejection")
            .WithSummary("Restore a rejected application back to UnderReview status so it can be reconsidered."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            var app = await admissionService.UndoRejectionAsync(id, userId, ct);
            var response = AdmissionResponseMapper.Map(app);
            await SendSuccessAsync(response, ct, "Application restored to Under Review successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Application not found", "not_found", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Cannot undo rejection", "invalid_state", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Failed to undo rejection", "undo_error", ex.Message, ct);
        }
    }
}
