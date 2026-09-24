using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;

namespace LMS.Api.Endpoints.Admissions;

public sealed class IdentifyEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<IdentifyRequest, AdmissionApplicationResponse?>
{
    public override void Configure()
    {
        Post("admissions/identify");
        AllowAnonymous();
        Description(d => d
            .WithName("Identify Applicant") 
            .WithTags("Admissions")
            .WithSummary("Identify a potential applicant by email and JAMB registration number"));
    }

    public override async Task HandleAsync(IdentifyRequest req, CancellationToken ct)
    {
        try
        {
            var app = await admissionService.VerifyIdentityAsync(req.Email, req.JambRegNumber);

            if (app == null)
            {
                await SendSuccessAsync(null, ct, "No record found");
                return;
            }

            var response = AdmissionResponseMapper.Map(app);

            await SendSuccessAsync(response, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Identification Failed", "internal_error", ex.Message, ct);
        }
    }
}
