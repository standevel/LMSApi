using FastEndpoints;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Curricula;

public sealed class UpdateCurriculumEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<UpdateCurriculumRequestWrapper, CurriculumDto>
{
    public override void Configure()
    {
        Put("admin/curricula/{Id}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Update curriculum metadata/name";
            s.Description = "Updates metadata properties like name, minimum credit units, and admission session for a curriculum.";
            s.Responses[200] = "Curriculum updated successfully.";
            s.Responses[404] = "Curriculum not found.";
        });
    }

    public override async Task HandleAsync(UpdateCurriculumRequestWrapper req, CancellationToken ct)
    {
        var requestDto = new UpdateCurriculumRequest(req.AdmissionSessionId, req.Name, req.MinCreditUnitsForGraduation);
        var result = await curriculumService.UpdateCurriculumAsync(req.Id, requestDto, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class UpdateCurriculumRequestWrapper
{
    public Guid Id { get; set; }
    public Guid AdmissionSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinCreditUnitsForGraduation { get; set; }
}
