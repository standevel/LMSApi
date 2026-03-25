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
        Get("/api/admissions/letter-templates/{type}");
        Policies(LmsPolicies.Management);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var type = Route<string>("type");
        var result = await letterService.GetTemplateByTypeAsync(type ?? "Undergraduate");
        await SendSuccessAsync(result, ct);
    }
}
