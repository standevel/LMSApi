using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class CreateSponsorRequest
{ 
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public sealed class CreateSponsorResponse
{
    public Guid Id { get; }
    public string Name { get; }
    public string Code { get; }

    public CreateSponsorResponse(Guid id, string name, string code)
    {
        Id = id;
        Name = name;
        Code = code;
    }
}

public sealed class CreateSponsorEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<CreateSponsorRequest, CreateSponsorResponse>
{
    public override void Configure()
    {
        Post("admissions/sponsors");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Create Sponsor") 
            .WithTags("Admissions")
            .WithSummary("Create a new sponsor organization for admission"));
    }

    public override async Task HandleAsync(CreateSponsorRequest req, CancellationToken ct)
    {
        var trimmedName = req.Name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            await SendFailureAsync(400, "Name is required", "validation_error", "Sponsor name cannot be empty.", ct);
            return;
        }

        var org = await admissionService.CreateSponsorAsync(trimmedName, req.Email?.Trim(), req.Phone?.Trim(), ct);
        await SendSuccessAsync(new CreateSponsorResponse(org.Id, org.Name, org.Code), ct);
    }
}
