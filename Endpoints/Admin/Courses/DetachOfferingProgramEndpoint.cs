using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class DetachOfferingProgramEndpoint(ICourseService courseService)
    : ApiEndpoint<DetachOfferingProgramRequest, CourseOfferingDto>
{
    public override void Configure()
    {
        Delete("admin/course-offerings/{id}/programs");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary     = "Detach a program+level from a course offering";
            s.Description = "Removes the link between a program/level and the course offering.";
            s.Responses[200] = "Program detached successfully.";
            s.Responses[404] = "Program attachment not found.";
        });
    }

    public override async Task HandleAsync(DetachOfferingProgramRequest req, CancellationToken ct)
    {
        var offeringId = Route<Guid>("id");
        var result = await courseService.DetachProgramAsync(offeringId, req.ProgramId, req.LevelId, ct);
        await result.Match(
            data   => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct));
    }
}
