using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class GetCurriculumByIdEndpoint(ICurriculumService curriculumService)
    : ApiEndpoint<GetCurriculumByIdRequest, CurriculumDto>
{
    public override void Configure()
    {
        Get("/api/admin/curricula/{Id}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Get a curriculum by ID";
            s.Description = "Retrieves the full details of a specific curriculum, including the academic program, admission session, and the complete schedule of courses divided by level and semester.";
            s.Responses[200] = "Successfully retrieved the curriculum details.";
            s.Responses[404] = "The specified curriculum ID was not found.";
        });
    }

    public override async Task HandleAsync(GetCurriculumByIdRequest req, CancellationToken ct)
    {
        var result = await curriculumService.GetByIdAsync(req.Id, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public record GetCurriculumByIdRequest(Guid Id);
