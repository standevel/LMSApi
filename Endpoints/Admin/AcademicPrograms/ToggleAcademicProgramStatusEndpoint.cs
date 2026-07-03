using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Security;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class ToggleAcademicProgramStatusEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<ToggleAcademicProgramStatusRequest, AcademicProgramDto>
{
    public override void Configure()
    {
        Patch("admin/programs/{id}/toggle-status");
        Group<AdminGroup>();
        Policies(LmsPolicies.AcademicManagement);
        Summary(s =>
        {
            s.Summary = "Toggle academic program status";
            s.Description = "Activates or deactivates an academic program. Inactive programs may not be available for new enrollments or session configurations.";
            s.Response<ApiResponse<AcademicProgramDto>>(200, "Successfully toggled the program status.");
            s.Response<ApiResponse<object>>(404, "The specified program ID was not found.");
        });
    }

    public override async Task HandleAsync(ToggleAcademicProgramStatusRequest req, CancellationToken ct)
    {
        var result = await programService.ToggleStatusAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
