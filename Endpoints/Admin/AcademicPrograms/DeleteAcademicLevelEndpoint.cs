using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class DeleteAcademicLevelEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<DeleteAcademicLevelRequestWrapper, AcademicProgramDto>
{
    public override void Configure()
    {
        Delete("admin/programs/{ProgramId}/levels/{LevelId}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Summary(s =>
        {
            s.Summary = "Delete an academic level from a program";
            s.Description = "Removes an academic level from a program, verifying first that it does not contain courses or enrollments.";
            s.Responses[200] = "Successfully deleted the academic level.";
            s.Responses[400] = "Validation error (e.g. level contains courses or enrolled students).";
            s.Responses[404] = "The specified academic program or level was not found.";
        });
    }

    public override async Task HandleAsync(DeleteAcademicLevelRequestWrapper req, CancellationToken ct)
    {
        var result = await programService.DeleteLevelAsync(req.ProgramId, req.LevelId, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class DeleteAcademicLevelRequestWrapper
{
    public Guid ProgramId { get; set; }
    public Guid LevelId { get; set; }
}
