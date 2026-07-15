using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class AddAcademicLevelEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<AddAcademicLevelRequestWrapper, AcademicProgramDto>
{
    public override void Configure()
    {
        Post("admin/programs/{ProgramId}/levels");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Summary(s =>
        {
            s.Summary = "Add an academic level to a program";
            s.Description = "Creates a new academic level (e.g. 500 Level) with standard semesters and max credit load configurations under an existing academic program.";
            s.Responses[200] = "Successfully added the academic level.";
            s.Responses[404] = "The specified academic program was not found.";
        });
    }

    public override async Task HandleAsync(AddAcademicLevelRequestWrapper req, CancellationToken ct)
    {
        var request = new AddAcademicLevelRequest(
            req.Name,
            req.Order,
            req.Semester1MaxCreditLoad,
            req.Semester2MaxCreditLoad);

        var result = await programService.AddLevelAsync(req.ProgramId, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class AddAcademicLevelRequestWrapper
{
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public int Semester1MaxCreditLoad { get; set; } = 24;
    public int Semester2MaxCreditLoad { get; set; } = 24;
}
