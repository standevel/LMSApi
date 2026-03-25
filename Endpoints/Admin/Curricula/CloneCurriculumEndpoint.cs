using FastEndpoints;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Curricula;

public sealed class CloneCurriculumEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<CloneCurriculumRequestWrapper, CurriculumDto>
{
    public override void Configure()
    {
        Post("/api/admin/curricula/{id}/clone");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Clone an existing curriculum";
            s.Description = "Creates a new draft curriculum based on an existing one, including all mapped courses.";
            s.Responses[200] = "Curriculum cloned successfully.";
            s.Responses[404] = "Source curriculum not found.";
        });
    }

    public override async Task HandleAsync(CloneCurriculumRequestWrapper req, CancellationToken ct)
    {
        var result = await curriculumService.CloneCurriculumAsync(req.Id, req.NewName, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class CloneCurriculumRequestWrapper
{
    public Guid Id { get; set; }
    public string NewName { get; set; } = string.Empty;
}
