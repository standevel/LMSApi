using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetLetterTemplateEndpoint(ILetterTemplateService letterService)
    : ApiEndpointWithoutRequest<LetterTemplateResponse?>
{
    public override void Configure()
    {
        Get("admissions/letter-templates/{type}");
        Policies(LmsPolicies.Management);
        Tags("Admissions");
        Description(d => d
            .WithName("Get Letter Template") 
            .WithTags("Admissions")
            .WithSummary("Retrieve the letter template for a specific admission type (e.g., Undergraduate, Postgraduate)"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var type = Route<string>("type");
        var result = await letterService.GetTemplateByTypeAsync(type ?? "Undergraduate");
        await SendSuccessAsync(result, ct);
    }
}
