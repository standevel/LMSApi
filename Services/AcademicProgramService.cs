using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Services;

public sealed class AcademicProgramService(
    IAcademicProgramRepository programRepository,
    IAuditService auditService) : BaseService(auditService), IAcademicProgramService
{
    public async Task<ErrorOr<AcademicProgramDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(id, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        return program.ToDto();
    }

    public async Task<ErrorOr<List<AcademicProgramDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var programs = await programRepository.GetAllAsync(ct);
        return programs.Select(p => p.ToDto()).ToList();
    }

    public async Task<ErrorOr<AcademicProgramDto>> CreateAsync(CreateAcademicProgramRequest request, CancellationToken ct = default)
    {
        if (await programRepository.ExistsByCodeAsync(request.Code, ct))
            return DomainErrors.AcademicProgram.DuplicateCode;

        var program = new AcademicProgram
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            // Ensure a non-null value for DegreeAwarded to avoid DB NOT NULL errors
            DegreeAwarded = request.DegreeAwarded ?? string.Empty,
            FacultyId = request.FacultyId,
            Type = request.Type,
            DurationYears = request.DurationYears,
            MinJambScore = request.MinJambScore,
            MaxAdmissions = request.MaxAdmissions,
            RequiredJambSubjectsJson = request.RequiredJambSubjectsJson,
            RequiredOLevelSubjectsJson = request.RequiredOLevelSubjectsJson,
            IsActive = true,
            Levels = new List<AcademicLevel>()
        };

        // Create levels and attach back-reference to the parent program so EF sets FK correctly
        foreach (var l in request.Levels)
        {
            var level = new AcademicLevel
            {
                Name = l.Name,
                Order = l.Order,
                Program = program
            };

            level.Semesters = l.Semesters.Select(s => new LevelSemesterConfig
            {
                Semester = s.Semester,
                MaxCreditLoad = s.MaxCreditLoad,
                Level = level
            }).ToList();

            program.Levels.Add(level);
        }

        await programRepository.AddAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);

        await LogActionAsync("Create", "AcademicProgram", program.Id.ToString(), $"Created program: {program.Name} ({program.Code})", ct);

        // Fetch again to ensure all navigation properties (like Faculty) are loaded
        var createdProduct = await programRepository.GetByIdAsync(program.Id, ct);
        return createdProduct!.ToDto();
    }

    public async Task<ErrorOr<AcademicProgramDto>> UpdateAsync(Guid id, UpdateAcademicProgramRequest request, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(id, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        program.Name = request.Name;
        program.Code = request.Code;
        program.Description = request.Description;
        // Guard against null to match DB NOT NULL constraint
        program.DegreeAwarded = request.DegreeAwarded ?? string.Empty;
        program.FacultyId = request.FacultyId;
        program.Type = request.Type;
        program.DurationYears = request.DurationYears;
        program.MinJambScore = request.MinJambScore;
        program.MaxAdmissions = request.MaxAdmissions;
        program.RequiredJambSubjectsJson = request.RequiredJambSubjectsJson;
        program.RequiredOLevelSubjectsJson = request.RequiredOLevelSubjectsJson;

        await programRepository.UpdateAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);

        await LogActionAsync("Update", "AcademicProgram", id.ToString(), $"Updated program: {program.Name}", ct);

        // Fetch again to ensure all navigation properties (like Faculty) are loaded
        var updatedProduct = await programRepository.GetByIdAsync(id, ct);
        return updatedProduct!.ToDto();
    }

    public async Task<ErrorOr<AcademicProgramDto>> ToggleStatusAsync(Guid id, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(id, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        program.IsActive = !program.IsActive;

        await programRepository.UpdateAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);

        await LogActionAsync("ToggleStatus", "AcademicProgram", id.ToString(), $"Program {(program.IsActive ? "activated" : "deactivated")}", ct);

        // Fetch again to ensure all navigation properties (like Faculty) are loaded
        var updatedProduct = await programRepository.GetByIdAsync(id, ct);
        return updatedProduct!.ToDto();
    }
}
