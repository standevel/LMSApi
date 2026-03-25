using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class UpdateCurriculumCourseEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<UpdateCourseRequestWrapper, CurriculumDto>
{
    public override void Configure()
    {
        Put("/api/admin/curricula/{CurriculumId}/courses/{Id}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Update a curriculum course mapping";
            s.Description = "Updates the level, semester, category, or credit units for a course already in a curriculum.";
            s.Responses[200] = "Successfully updated the curriculum course.";
            s.Responses[404] = "The specified curriculum or mapping was not found.";
        });
    }

    public override async Task HandleAsync(UpdateCourseRequestWrapper req, CancellationToken ct)
    {
        var request = new UpdateCurriculumCourseRequest(
            req.LevelId,
            req.Semester,
            req.Category,
            req.CreditUnits);

        var result = await curriculumService.UpdateCourseAsync(req.CurriculumId, req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class UpdateCourseRequestWrapper
{
    public Guid CurriculumId { get; set; }
    public Guid Id { get; set; }
    public Guid LevelId { get; set; }
    public LMS.Api.Data.Enums.Semester Semester { get; set; }
    public LMS.Api.Data.Enums.CourseCategory Category { get; set; }
    public int CreditUnits { get; set; }
}
