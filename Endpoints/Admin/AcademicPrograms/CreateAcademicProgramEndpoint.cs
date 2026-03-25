using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class CreateAcademicProgramEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<CreateAcademicProgramRequest, AcademicProgramDto>
{
    public override void Configure()
    {
        Post("/api/admin/programs");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Create an academic program";
            s.Description = "Registers a new academic program (e.g., Computer Science) specifying its code, degree awarded, and standard academic levels.";
            s.Responses[200] = "Successfully created the academic program.";
            s.Responses[400] = "Validation failed, likely due to a duplicate program code.";
        });
    }

    public override async Task HandleAsync(CreateAcademicProgramRequest req, CancellationToken ct)
    {
        var result = await programService.CreateAsync(req, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
