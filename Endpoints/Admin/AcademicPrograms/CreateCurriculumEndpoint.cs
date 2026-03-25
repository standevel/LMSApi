using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class CreateCurriculumEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<CreateCurriculumRequestWrapper, CurriculumDto>
{
    public override void Configure()
    {
        Post("/api/admin/programs/{ProgramId}/curricula");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Create a curriculum";
            s.Description = "Establishes a new curriculum mapping for a specific academic program and admission session. Defines the graduation requirements.";
            s.Responses[200] = "Successfully created the curriculum.";
            s.Responses[404] = "The specified academic program was not found.";
        });
    }

    public override async Task HandleAsync(CreateCurriculumRequestWrapper req, CancellationToken ct)
    {
        var request = new CreateCurriculumRequest(
            req.AdmissionSessionId,
            req.Name,
            req.MinCreditUnitsForGraduation);

        var result = await curriculumService.CreateCurriculumAsync(req.ProgramId, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class CreateCurriculumRequestWrapper
{
    public Guid ProgramId { get; set; }
    public Guid AdmissionSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinCreditUnitsForGraduation { get; set; }
}
