using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class ListAcademicProgramsEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<EmptyRequest, List<AcademicProgramDto>>
{
    public override void Configure()
    {
        Get("/api/admin/programs");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "List academic programs";
            s.Description = "Retrieves a complete list of all academic programs, including their associated levels and active status.";
            s.Responses[200] = "Successfully retrieved the list of academic programs.";
        });
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var result = await programService.GetAllAsync(ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}
