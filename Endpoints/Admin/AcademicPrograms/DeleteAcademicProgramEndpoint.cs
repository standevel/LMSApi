using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class DeleteAcademicProgramRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteAcademicProgramEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<DeleteAcademicProgramRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("admin/programs/{id}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Summary(s =>
        {
            s.Summary = "Delete an academic program";
            s.Description = "Deletes an academic program if it has no curriculum and no enrollments.";
            s.Responses[204] = "Successfully deleted.";
            s.Responses[400] = "Validation failed (e.g., has curriculum).";
        });
    }

    public override async Task HandleAsync(DeleteAcademicProgramRequest req, CancellationToken ct)
    {
        var result = await programService.DeleteAsync(req.Id, ct);

        await result.Match(
            _ => SendSuccessAsync(new EmptyResponse(), ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
