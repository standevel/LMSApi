using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;
using System.Collections.Generic;

namespace LMS.Api.Endpoints.Fees;

public sealed class CreateScholarshipEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpoint<CreateScholarshipRequest, ScholarshipDto>
{
    public override void Configure()
    {
        Post("fees/scholarships");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(CreateScholarshipRequest req, CancellationToken ct)
    {
        var result = await scholarshipService.CreateScholarshipAsync(req);
        await SendSuccessAsync(result, ct);
    }
}

public sealed class UpdateScholarshipEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpoint<UpdateScholarshipRequest, ScholarshipDto>
{
    public override void Configure()
    {
        Put("fees/scholarships/{Id}");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(UpdateScholarshipRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("Id");
        var result = await scholarshipService.UpdateScholarshipAsync(id, req);
        if (result == null)
            await SendFailureAsync(404, "Not Found", "NOT_FOUND", "Scholarship not found", ct);
        else
            await SendSuccessAsync(result, ct);
    }
}

public sealed class GetScholarshipsEndpoint(IScholarshipService scholarshipService) 
    : ApiEndpoint<EmptyRequest, IEnumerable<ScholarshipDto>>
{
    public override void Configure()
    {
        Get("fees/scholarships");
        Policies(LmsPolicies.FinanceManagement);
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await scholarshipService.GetAllScholarshipsAsync();
        await SendSuccessAsync(result, ct);
    }
}
