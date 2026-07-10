using FastEndpoints;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Curricula;

public sealed class RemoveCurriculumLevelEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<RemoveCurriculumLevelRequest, CurriculumDto>
{
    public override void Configure()
    {
        Delete("admin/curricula/{CurriculumId}/levels/{LevelId}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Delete all courses of a level in curriculum";
            s.Description = "Removes all mapped courses for a specific level from the curriculum.";
            s.Responses[200] = "Courses removed successfully.";
            s.Responses[404] = "Curriculum not found.";
        });
    }

    public override async Task HandleAsync(RemoveCurriculumLevelRequest req, CancellationToken ct)
    {
        var result = await curriculumService.RemoveLevelAsync(req.CurriculumId, req.LevelId, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class RemoveCurriculumLevelRequest
{
    public Guid CurriculumId { get; set; }
    public Guid LevelId { get; set; }
}
