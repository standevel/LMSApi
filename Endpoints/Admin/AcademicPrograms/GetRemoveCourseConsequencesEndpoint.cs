using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class GetRemoveCourseConsequencesEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<GetRemoveCourseConsequencesRequest, RemoveCourseConsequencesDto>
{
    public override void Configure()
    {
        Get("admin/curricula/{CurriculumId}/courses/{Id}/consequences");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Get the consequences of removing a course from a curriculum";
            s.Description = "Returns details on active offerings, student enrollments, and grades that would be affected.";
            s.Responses[200] = "Successfully retrieved the consequences.";
            s.Responses[404] = "The specified curriculum or mapping was not found.";
        });
    }

    public override async Task HandleAsync(GetRemoveCourseConsequencesRequest req, CancellationToken ct)
    {
        var result = await curriculumService.GetRemoveCourseConsequencesAsync(req.CurriculumId, req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class GetRemoveCourseConsequencesRequest
{
    public Guid CurriculumId { get; set; }
    public Guid Id { get; set; }
}
