using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class UpdateCourseEndpoint(ICourseService courseService)
    : ApiEndpoint<UpdateCourseRequestWrapper, CourseDto>
{
    public override void Configure()
    {
        Put("admin/courses/{id}");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Update a course";
            s.Description = "Updates the details of an existing course, including its title, description, credit units, and offerings. Replaces existing offerings with the provided list.";
            s.Responses[200] = "Course details successfully updated.";
            s.Responses[404] = "The specified course ID could not be found.";
        });
    }

    public override async Task HandleAsync(UpdateCourseRequestWrapper req, CancellationToken ct)
    {
        var request = new UpdateCourseRequest(
            req.Code,
            req.Title,
            req.Description,
            req.CreditUnits,
            req.LevelId,
            req.Semester,
            req.Offerings);

        var result = await courseService.UpdateAsync(req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class UpdateCourseRequestWrapper
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CreditUnits { get; set; }
    public Guid? LevelId { get; set; }
    public LMS.Api.Data.Enums.Semester? Semester { get; set; }
    public List<CreateCourseOfferingRequest> Offerings { get; set; } = [];
}
