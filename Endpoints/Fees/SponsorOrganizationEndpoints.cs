using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Fees;

public sealed class CreateSponsorOrganizationEndpoint(ISponsorOrganizationService sponsorService) 
    : ApiEndpoint<CreateSponsorOrganizationRequest, SponsorOrganizationDto>
{
    public override void Configure()
    {
        Post("fees/sponsors");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(CreateSponsorOrganizationRequest req, CancellationToken ct)
    {
        var result = await sponsorService.CreateSponsorAsync(req);
        await SendSuccessAsync(result, ct);
    }
}

public sealed class UpdateSponsorOrganizationEndpoint(ISponsorOrganizationService sponsorService) 
    : ApiEndpoint<UpdateSponsorOrganizationRequest, SponsorOrganizationDto>
{
    public override void Configure()
    {
        Put("fees/sponsors/{Id}");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(UpdateSponsorOrganizationRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("Id");
        var result = await sponsorService.UpdateSponsorAsync(id, req);
        
        if (result == null)
            await SendFailureAsync(404, "Not Found", "NOT_FOUND", "Sponsor Organization not found", ct);
        else
            await SendSuccessAsync(result, ct);
    }
}

public sealed class GetSponsorOrganizationsEndpoint(ISponsorOrganizationService sponsorService) 
    : ApiEndpoint<GetSponsorsRequest, IEnumerable<SponsorOrganizationDto>>
{
    public override void Configure()
    {
        Get("fees/sponsors");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(GetSponsorsRequest req, CancellationToken ct)
    {
        var results = await sponsorService.GetSponsorsAsync(req.ActiveOnly);
        await SendSuccessAsync(results, ct);
    }
}
