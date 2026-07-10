using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.Courses;

public sealed class AttachOfferingProgramEndpoint(ICourseService courseService)
    : ApiEndpoint<AttachOfferingProgramRequest, CourseOfferingDto>
{
    public override void Configure()
    {
        Post("admin/course-offerings/{id}/programs");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary     = "Attach a program+level to a course offering";
            s.Description = "Links a program and academic level to an existing course offering. "
                          + "The same offering can serve multiple programs simultaneously.";
            s.Responses[200] = "Program attached successfully.";
            s.Responses[409] = "This program/level is already attached to the offering.";
            s.Responses[404] = "Course offering not found.";
        });
    }

    public override async Task HandleAsync(AttachOfferingProgramRequest req, CancellationToken ct)
    {
        var offeringId = Route<Guid>("id");
        var result = await courseService.AttachProgramAsync(offeringId, req.ProgramId, req.LevelId, ct);
        await result.Match(
            data   => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct));
    }
}
