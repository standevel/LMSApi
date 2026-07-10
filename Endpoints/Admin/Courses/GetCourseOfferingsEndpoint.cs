using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class GetCourseOfferingsRequest
{
    public Guid? AcademicSessionId { get; set; }
}

public sealed class GetCourseOfferingsEndpoint(ICourseService courseService)
    : ApiEndpoint<GetCourseOfferingsRequest, List<CourseOfferingDto>>
{
    public override void Configure()
    {
        Get("admin/course-offerings");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "List all course offerings";
            s.Description = "Returns all course offerings with their current lecturer assignments. Optionally filter by academic session.";
            s.Responses[200] = "Course offerings retrieved successfully.";
        });
    }

    public override async Task HandleAsync(GetCourseOfferingsRequest req, CancellationToken ct)
    {
        var result = await courseService.GetCourseOfferingsAsync(req.AcademicSessionId, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
