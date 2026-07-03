using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Contracts;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class EnrollStudentEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<EnrollStudentRequest, EnrollmentDto>
{
    public override void Configure()
    {
        Post("admin/programs/enroll");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Enroll a student";
            s.Description = "Enrolls a student into a specific academic program under a given curriculum and academic session.";
            s.Response<ApiResponse<EnrollmentDto>>(200, "Successfully enrolled the student.");
            s.Response<ApiResponse<object>>(400, "Validation error, such as a duplicate enrollment for the same session.");
        });
    }

    public override async Task HandleAsync(EnrollStudentRequest req, CancellationToken ct)
    {
        var result = await curriculumService.EnrollStudentAsync(req, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
