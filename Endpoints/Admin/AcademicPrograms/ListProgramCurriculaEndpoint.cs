using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class ListProgramCurriculaEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<ListProgramCurriculaRequest, List<CurriculumSummaryDto>>
{
    public override void Configure()
    {
        Get("/api/admin/programs/{ProgramId}/curricula");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "List program curricula";
            s.Description = "Retrieves a summary list of all curricula mapped to a specific academic program. Useful for viewing historical and current curriculum revisions for a program.";
            s.Responses[200] = "Successfully retrieved the list of curricula.";
        });
    }

    public override async Task HandleAsync(ListProgramCurriculaRequest req, CancellationToken ct)
    {
        var result = await curriculumService.GetByProgramIdAsync(req.ProgramId, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public record ListProgramCurriculaRequest(Guid ProgramId);
