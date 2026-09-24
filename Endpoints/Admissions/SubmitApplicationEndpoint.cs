using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Entities;

namespace LMS.Api.Endpoints.Admissions;

public sealed class SubmitApplicationEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<SubmitApplicationRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Post("admissions/submit/{Id}");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Submit Application") 
            .WithTags("Admissions")
            .WithSummary("Submit a draft admission application for review"));
    }

    public override async Task HandleAsync(SubmitApplicationRequest req, CancellationToken ct)
    {
        try
        {
            var app = await admissionService.SubmitApplicationAsync(req.Id);

            var response = AdmissionResponseMapper.Map(app);

            await SendSuccessAsync(response, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Application Not Found", "not_found", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Incomplete Application", "missing_requirements", ex.Message, ct);
        }
    }
}
