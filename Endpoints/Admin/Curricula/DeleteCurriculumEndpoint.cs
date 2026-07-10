using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

using LMS.Api.Endpoints.Admin;

namespace LMS.Api.Endpoints.Admin.Curricula;

public sealed class DeleteCurriculumRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteCurriculumEndpoint(ICurriculumService curriculumService) : ApiEndpoint<DeleteCurriculumRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("admin/curricula/{id}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
    }

    public override async Task HandleAsync(DeleteCurriculumRequest req, CancellationToken ct)
    {
        var result = await curriculumService.DeleteCurriculumAsync(req.Id, ct);

        await result.Match(
            _ => SendSuccessAsync(new EmptyResponse(), ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
