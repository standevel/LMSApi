using FastEndpoints;
using LMS.Api.Common.Extensions;
using LMS.Api.Endpoints.Admin;
using LMS.Api.Contracts;
using LMS.Api.Services;
using LMS.Api.Data.Enums;

namespace LMS.Api.Endpoints.Admin.AcademicPrograms;

public sealed class UpdateAcademicProgramEndpoint(IAcademicProgramService programService)
    : ApiEndpoint<UpdateAcademicProgramRequestWrapper, AcademicProgramDto>
{
    public override void Configure()
    {
        Put("/api/admin/programs/{id}");
        Group<AdminGroup>();
        Summary(s =>
        {
            s.Summary = "Update an academic program";
            s.Description = "Modifies existing details for an academic program by its ID. Can update the name, description, and degree awarded.";
            s.Responses[200] = "Program updated successfully.";
            s.Responses[404] = "An academic program with the specified ID was not found.";
        });
    }

    public override async Task HandleAsync(UpdateAcademicProgramRequestWrapper req, CancellationToken ct)
    {
        var request = new UpdateAcademicProgramRequest(
            req.Name,
            req.Code,
            req.Description,
            req.DegreeAwarded,
            req.FacultyId,
            req.Type,
            req.DurationYears,
            req.MinJambScore,
            req.MaxAdmissions,
            req.RequiredJambSubjectsJson,
            req.RequiredOLevelSubjectsJson);

        var result = await programService.UpdateAsync(req.Id, request, ct);
        await result.Match(
            data => SendSuccessAsync(data, ct),
            errors => HandleErrorAsync(errors, ct)
        );
    }
}

public class UpdateAcademicProgramRequestWrapper
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DegreeAwarded { get; set; } = string.Empty;
    public Guid FacultyId { get; set; }
    public ProgramType Type { get; set; }
    public int DurationYears { get; set; }
    public int MinJambScore { get; set; }
    public int MaxAdmissions { get; set; }
    public string RequiredJambSubjectsJson { get; set; } = "[]";
    public string RequiredOLevelSubjectsJson { get; set; } = "[]";
}
