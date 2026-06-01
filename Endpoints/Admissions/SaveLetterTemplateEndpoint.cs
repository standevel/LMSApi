using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admissions;

public sealed class SaveLetterTemplateEndpoint(ILetterTemplateService letterService)
    : ApiEndpoint<SaveLetterTemplateRequest, LetterTemplateResponse>
{
    public override void Configure()
    {
        Post("admissions/letter-templates");
        Policies(LmsPolicies.Management);
        Tags("Admissions");
        Description(d => d
            .WithName("Save Letter Template") 
            .WithTags("Admissions")
            .WithSummary("Create or update a letter template for a specific admission type"));
    }

    public override async Task HandleAsync(SaveLetterTemplateRequest req, CancellationToken ct)
    {
        var result = await letterService.SaveTemplateAsync(req);
        await SendSuccessAsync(result, ct, "Template saved successfully");
    }
}
