using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetSponsorsEndpoint(IAdmissionService admissionService) : ApiEndpointWithoutRequest<IEnumerable<SponsorOrganizationResponse>>
{
    public override void Configure()
    {
        Get("admissions/sponsors");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Get Sponsors") 
            .WithTags("Admissions")
            .WithSummary("Retrieve all sponsor organizations available for admission"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sponsors = await admissionService.GetAdmissionSponsorsAsync();
        var response = sponsors.Select(s => new SponsorOrganizationResponse(s.Id, s.Name, s.Code));
        await SendSuccessAsync(response, ct);
    }
}
