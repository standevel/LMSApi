using FastEndpoints;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Curricula;

public sealed class PublishCurriculumEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<PublishCurriculumRequest, CurriculumDto>
{
    public override void Configure()
    {
        Post("/api/admin/curricula/{id}/publish");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Publish a draft curriculum";
            s.Description = "Transitions a curriculum from Draft to Published status after validating prerequisites.";
            s.Responses[200] = "Curriculum published successfully.";
            s.Responses[400] = "Validation failed (e.g., circular dependencies).";
            s.Responses[404] = "Curriculum not found.";
        });
    }

    public override async Task HandleAsync(PublishCurriculumRequest req, CancellationToken ct)
    {
        var result = await curriculumService.PublishCurriculumAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class PublishCurriculumRequest
{
    public Guid Id { get; set; }
}
