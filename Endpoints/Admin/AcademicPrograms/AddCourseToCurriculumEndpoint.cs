using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class AddCourseToCurriculumEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<AddCurriculumCourseRequestWrapper, CurriculumDto>
{
    public override void Configure()
    {
        Post("/api/admin/curricula/{CurriculumId}/courses");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Add a course to a curriculum";
            s.Description = "Maps an existing course to a target curriculum, specifying the level of study and the semester the course should be taken.";
            s.Responses[200] = "Successfully added the course to the curriculum.";
            s.Responses[404] = "The specified curriculum or course was not found.";
        });
    }

    public override async Task HandleAsync(AddCurriculumCourseRequestWrapper req, CancellationToken ct)
    {
        var request = new AddCurriculumCourseRequest(
            req.LevelId,
            req.CourseId,
            req.Semester,
            req.Category,
            req.CreditUnits);

        var result = await curriculumService.AddCourseAsync(req.CurriculumId, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class AddCurriculumCourseRequestWrapper
{
    public Guid CurriculumId { get; set; }
    public Guid LevelId { get; set; }
    public Guid CourseId { get; set; }
    public LMS.Api.Data.Enums.Semester Semester { get; set; }
    public LMS.Api.Data.Enums.CourseCategory Category { get; set; }
    public int CreditUnits { get; set; }
}
