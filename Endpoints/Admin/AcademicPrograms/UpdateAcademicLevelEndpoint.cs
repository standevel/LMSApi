using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class UpdateAcademicLevelEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<UpdateAcademicLevelRequestWrapper, AcademicProgramDto>
{
    public override void Configure()
    {
        Put("admin/programs/{ProgramId}/levels/{LevelId}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Summary(s =>
        {
            s.Summary = "Update an academic level in a program";
            s.Description = "Modifies an existing academic level's details and semester max credit load configurations.";
            s.Responses[200] = "Successfully updated the academic level.";
            s.Responses[404] = "The specified academic program or level was not found.";
        });
    }

    public override async Task HandleAsync(UpdateAcademicLevelRequestWrapper req, CancellationToken ct)
    {
        var request = new UpdateAcademicLevelRequest(
            req.Name,
            req.Order,
            req.Semester1MaxCreditLoad,
            req.Semester2MaxCreditLoad);

        var result = await programService.UpdateLevelAsync(req.ProgramId, req.LevelId, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class UpdateAcademicLevelRequestWrapper
{
    public Guid ProgramId { get; set; }
    public Guid LevelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public int Semester1MaxCreditLoad { get; set; } = 24;
    public int Semester2MaxCreditLoad { get; set; } = 24;
}
