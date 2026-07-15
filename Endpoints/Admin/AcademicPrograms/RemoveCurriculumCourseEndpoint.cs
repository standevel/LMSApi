using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class RemoveCurriculumCourseEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<RemoveCurriculumCourseRequest, CurriculumDto>
{
    public override void Configure()
    {
        Delete("admin/curricula/{CurriculumId}/courses/{Id}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Remove a course from a curriculum";
            s.Description = "Removes a specific course mapping from the curriculum.";
            s.Responses[200] = "Successfully removed the course from curriculum.";
            s.Responses[404] = "The specified curriculum or mapping was not found.";
        });
    }

    public override async Task HandleAsync(RemoveCurriculumCourseRequest req, CancellationToken ct)
    {
        var result = await curriculumService.RemoveCourseAsync(req.CurriculumId, req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class RemoveCurriculumCourseRequest
{
    public Guid CurriculumId { get; set; }
    public Guid Id { get; set; }
}
