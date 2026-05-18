using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AdmissionConfig;

public sealed class ListProgramPrerequisitesEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<IEnumerable<ProgramPrerequisiteDto>>
{
    public override void Configure()
    {
        Get("/api/admin/admission-config/program-prerequisites");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var prerequisites = await dbContext.ProgramPrerequisites
            .Include(p => p.Program)
            .OrderBy(p => p.Program!.Name)
            .ThenBy(p => p.RequiredSubjectCode)
            .ToListAsync(ct);

        var response = prerequisites.Select(p => new ProgramPrerequisiteDto(
            p.Id, p.ProgramId, p.Program?.Name, p.RequiredSubjectCode,
            p.RequiredSubjectName, p.MinGrade, p.IsCore, p.IsActive, p.CreatedAt));

        await SendSuccessAsync(response, ct);
    }
}

public record ProgramPrerequisiteDto(
    Guid Id,
    Guid ProgramId,
    string? ProgramName,
    string RequiredSubjectCode,
    string RequiredSubjectName,
    string MinGrade,
    bool IsCore,
    bool IsActive,
    DateTime CreatedAt);

public sealed class CreateProgramPrerequisiteEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateProgramPrerequisiteRequest, ProgramPrerequisiteDto>
{
    public override void Configure()
    {
        Post("/api/admin/admission-config/program-prerequisites");
    }

    public override async Task HandleAsync(CreateProgramPrerequisiteRequest req, CancellationToken ct)
    {
        var prerequisite = new ProgramPrerequisite
        {
            ProgramId = req.ProgramId,
            RequiredSubjectCode = req.RequiredSubjectCode,
            RequiredSubjectName = req.RequiredSubjectName,
            MinGrade = req.MinGrade,
            IsCore = req.IsCore,
            IsActive = req.IsActive
        };
        dbContext.ProgramPrerequisites.Add(prerequisite);
        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new ProgramPrerequisiteDto(
            prerequisite.Id, prerequisite.ProgramId, null, prerequisite.RequiredSubjectCode,
            prerequisite.RequiredSubjectName, prerequisite.MinGrade, prerequisite.IsCore,
            prerequisite.IsActive, prerequisite.CreatedAt), ct);
    }
}

public sealed class UpdateProgramPrerequisiteEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateProgramPrerequisiteRequest, ProgramPrerequisiteDto>
{
    public override void Configure()
    {
        Patch("/api/admin/admission-config/program-prerequisites/{Id}");
    }

    public override async Task HandleAsync(UpdateProgramPrerequisiteRequest req, CancellationToken ct)
    {
        var prerequisite = await dbContext.ProgramPrerequisites.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Program prerequisite not found");

        prerequisite.ProgramId = req.ProgramId ?? prerequisite.ProgramId;
        prerequisite.RequiredSubjectCode = req.RequiredSubjectCode ?? prerequisite.RequiredSubjectCode;
        prerequisite.RequiredSubjectName = req.RequiredSubjectName ?? prerequisite.RequiredSubjectName;
        prerequisite.MinGrade = req.MinGrade ?? prerequisite.MinGrade;
        prerequisite.IsCore = req.IsCore ?? prerequisite.IsCore;
        prerequisite.IsActive = req.IsActive ?? prerequisite.IsActive;

        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new ProgramPrerequisiteDto(
            prerequisite.Id, prerequisite.ProgramId, null, prerequisite.RequiredSubjectCode,
            prerequisite.RequiredSubjectName, prerequisite.MinGrade, prerequisite.IsCore,
            prerequisite.IsActive, prerequisite.CreatedAt), ct);
    }
}

public sealed class DeleteProgramPrerequisiteEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteProgramPrerequisiteRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("/api/admin/admission-config/program-prerequisites/{Id}");
    }

    public override async Task HandleAsync(DeleteProgramPrerequisiteRequest req, CancellationToken ct)
    {
        var prerequisite = await dbContext.ProgramPrerequisites.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Program prerequisite not found");

        prerequisite.IsActive = false;
        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new EmptyResponse(), ct);
    }
}

public record CreateProgramPrerequisiteRequest(
    Guid ProgramId,
    string RequiredSubjectCode,
    string RequiredSubjectName,
    string MinGrade,
    bool IsCore = true,
    bool IsActive = true);

public record UpdateProgramPrerequisiteRequest(
    Guid Id,
    Guid? ProgramId,
    string? RequiredSubjectCode,
    string? RequiredSubjectName,
    string? MinGrade,
    bool? IsCore,
    bool? IsActive);

public record DeleteProgramPrerequisiteRequest(Guid Id);
