using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class BulkAddCurriculumCoursesEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<BulkAddRequestWrapper, CurriculumDto>
{
    public override void Configure()
    {
        Post("/api/admin/curricula/{CurriculumId}/courses/bulk");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Batch add courses to a curriculum";
            s.Description = "Maps multiple existing courses to a target curriculum for a specific level and semester.";
            s.Responses[200] = "Successfully added the courses to the curriculum.";
            s.Responses[404] = "The specified curriculum was not found.";
        });
    }

    public override async Task HandleAsync(BulkAddRequestWrapper req, CancellationToken ct)
    {
        var request = new BulkAddCurriculumCourseRequest(
            req.LevelId,
            req.Semester,
            req.Selections);

        var result = await curriculumService.AddCoursesBulkAsync(req.CurriculumId, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class BulkAddRequestWrapper
{
    public Guid CurriculumId { get; set; }
    public Guid LevelId { get; set; }
    public LMS.Api.Data.Enums.Semester Semester { get; set; }
    public List<CourseSelectionDto> Selections { get; set; } = [];
}
