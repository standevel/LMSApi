using FastEndpoints;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.Curricula;

public sealed class AddCoursePrerequisiteEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<AddCoursePrerequisiteRequestWrapper, bool>
{
    public override void Configure()
    {
        Post("/api/admin/courses/{id}/prerequisites");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Add a prerequisite to a course";
            s.Description = "Maps a dependency between two courses. Validates against circular dependencies.";
            s.Responses[200] = "Prerequisite added successfully.";
            s.Responses[400] = "Circular dependency or invalid prerequisite detected.";
        });
    }

    public override async Task HandleAsync(AddCoursePrerequisiteRequestWrapper req, CancellationToken ct)
    {
        var request = new AddCoursePrerequisiteRequest(req.PrerequisiteCourseId, req.Type);
        var result = await curriculumService.AddPrerequisiteAsync(req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class AddCoursePrerequisiteRequestWrapper
{
    public Guid Id { get; set; }
    public Guid PrerequisiteCourseId { get; set; }
    public LMS.Api.Data.Enums.PrerequisiteType Type { get; set; }
}
