using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Common.Mapping;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class AcademicProgramService(
    LmsDbContext dbContext,
    IAcademicProgramRepository programRepository,
    ICurrentUserContext currentUserContext,
    IUserRoleRepository userRoleRepository,
    IAuditService auditService) : BaseService(auditService), IAcademicProgramService
{
    public async Task<ErrorOr<AcademicProgramDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(id, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        return program.ToDto();
    }

    private async Task<bool> IsUserAdminAsync(Guid userId, CancellationToken ct)
    {
        var roles = await userRoleRepository.GetRoleNamesAsync(userId, ct);
        return roles.Contains(LmsRoles.SuperAdmin) || roles.Contains(LmsRoles.Admin) || roles.Contains(LmsRoles.AcademicAdmin) || roles.Contains(LmsRoles.ViceChancellor);
    }

    private async Task<bool> IsUserDeanAsync(Guid userId, CancellationToken ct)
    {
        var roles = await userRoleRepository.GetRoleNamesAsync(userId, ct);
        return roles.Contains(LmsRoles.Dean);
    }

    private async Task<bool> IsUserHodAsync(Guid userId, CancellationToken ct)
    {
        var roles = await userRoleRepository.GetRoleNamesAsync(userId, ct);
        return roles.Contains(LmsRoles.HOD);
    }

    public async Task<ErrorOr<List<AcademicProgramDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var programs = await programRepository.GetAllAsync(ct);

        var userId = await currentUserContext.GetUserIdAsync(ct) ?? Guid.Empty;
        if (userId != Guid.Empty && !await IsUserAdminAsync(userId, ct))
        {
            bool isDean = await IsUserDeanAsync(userId, ct);
            bool isHod = await IsUserHodAsync(userId, ct);

            if (isDean && isHod)
            {
                programs = programs.Where(p => 
                    (p.Department?.Faculty?.DeanId == userId) || 
                    (p.Department?.HeadId == userId)).ToList();
            }
            else if (isDean)
            {
                programs = programs.Where(p => p.Department?.Faculty?.DeanId == userId).ToList();
            }
            else if (isHod)
            {
                programs = programs.Where(p => p.Department?.HeadId == userId).ToList();
            }
            // If they are not Admin, Dean, or HOD but have access, we return empty list or let it be?
            // Usually endpoints have [Authorize(Roles = ...)] so they won't reach here if not authorized,
            // but if they do (e.g. Lecturer), maybe they shouldn't see all programs unless they are Dean/HOD.
            else
            {
                programs = new List<AcademicProgram>();
            }
        }

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
            DepartmentId = request.DepartmentId,
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
        program.DepartmentId = request.DepartmentId;
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

    public async Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(id, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        var hasCourses = await dbContext.Courses.AnyAsync(c => c.ProgramId == id, ct);
        if (hasCourses)
            return Error.Validation("AcademicProgram.HasCourses", "Cannot delete a program that has courses attached.");

        var hasCurricula = await dbContext.Curricula.AnyAsync(c => c.ProgramId == id, ct);
        if (hasCurricula)
            return Error.Validation("AcademicProgram.HasCurricula", "Cannot delete a program that has curricula attached.");

        var hasEnrollments = await dbContext.Enrollments.AnyAsync(e => e.ProgramId == id, ct);
        if (hasEnrollments)
            return Error.Validation("AcademicProgram.HasEnrollments", "Cannot delete a program with enrolled students.");

        var hasStudents = await dbContext.Students.AnyAsync(s => s.AcademicProgramId == id, ct);
        if (hasStudents)
            return Error.Validation("AcademicProgram.HasStudents", "Cannot delete a program that has students attached.");

        var hasFeeTemplates = await dbContext.FeeTemplates.AnyAsync(f => f.ProgramId == id, ct);
        if (hasFeeTemplates)
            return Error.Validation("AcademicProgram.HasFeeTemplates", "Cannot delete a program that has fee templates attached.");

        var hasFeeAssignments = await dbContext.FeeAssignments.AnyAsync(f => f.ProgramId == id, ct);
        if (hasFeeAssignments)
            return Error.Validation("AcademicProgram.HasFeeAssignments", "Cannot delete a program that has fee assignments attached.");

        var hasCourseOfferings = await dbContext.CourseOfferingPrograms.AnyAsync(cop => cop.ProgramId == id, ct);
        if (hasCourseOfferings)
            return Error.Validation("AcademicProgram.HasCourseOfferings", "Cannot delete a program that has course offerings attached.");

        var hasAdmissionApplications = await dbContext.AdmissionApplications.AnyAsync(a => a.AcademicProgramId == id, ct);
        if (hasAdmissionApplications)
            return Error.Validation("AcademicProgram.HasAdmissionApplications", "Cannot delete a program that has admission applications attached.");

        await programRepository.DeleteAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);

        await LogActionAsync("Delete", "AcademicProgram", id.ToString(), $"Deleted program: {program.Name}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<AcademicProgramDto>> AddLevelAsync(Guid programId, AddAcademicLevelRequest request, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(programId, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        var level = new AcademicLevel
        {
            Name = request.Name,
            Order = request.Order,
            Program = program
        };

        level.Semesters = new List<LevelSemesterConfig>
        {
            new() { Semester = Semester.First, MaxCreditLoad = request.Semester1MaxCreditLoad, Level = level },
            new() { Semester = Semester.Second, MaxCreditLoad = request.Semester2MaxCreditLoad, Level = level }
        };

        program.Levels.Add(level);
        await programRepository.UpdateAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);

        await LogActionAsync("AddLevel", "AcademicProgram", programId.ToString(), $"Added academic level: {level.Name} (Order: {level.Order})", ct);

        var updatedProgram = await programRepository.GetByIdAsync(programId, ct);
        return updatedProgram!.ToDto();
    }

    public async Task<ErrorOr<AcademicProgramDto>> UpdateLevelAsync(Guid programId, Guid levelId, UpdateAcademicLevelRequest request, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(programId, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        var level = program.Levels.FirstOrDefault(l => l.Id == levelId);
        if (level is null) return Error.NotFound("AcademicLevel.NotFound", "The specified academic level was not found.");

        level.Name = request.Name;
        level.Order = request.Order;

        var sem1 = level.Semesters.FirstOrDefault(s => s.Semester == Semester.First);
        if (sem1 is not null)
        {
            sem1.MaxCreditLoad = request.Semester1MaxCreditLoad;
        }
        else
        {
            level.Semesters.Add(new LevelSemesterConfig { Semester = Semester.First, MaxCreditLoad = request.Semester1MaxCreditLoad, Level = level });
        }

        var sem2 = level.Semesters.FirstOrDefault(s => s.Semester == Semester.Second);
        if (sem2 is not null)
        {
            sem2.MaxCreditLoad = request.Semester2MaxCreditLoad;
        }
        else
        {
            level.Semesters.Add(new LevelSemesterConfig { Semester = Semester.Second, MaxCreditLoad = request.Semester2MaxCreditLoad, Level = level });
        }

        await programRepository.UpdateAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);

        await LogActionAsync("UpdateLevel", "AcademicProgram", programId.ToString(), $"Updated academic level: {level.Name}", ct);

        var updatedProgram = await programRepository.GetByIdAsync(programId, ct);
        return updatedProgram!.ToDto();
    }

    public async Task<ErrorOr<AcademicProgramDto>> DeleteLevelAsync(Guid programId, Guid levelId, CancellationToken ct = default)
    {
        var program = await programRepository.GetByIdAsync(programId, ct);
        if (program is null) return DomainErrors.AcademicProgram.NotFound;

        var level = program.Levels.FirstOrDefault(l => l.Id == levelId);
        if (level is null) return Error.NotFound("AcademicLevel.NotFound", "The specified academic level was not found.");

        var hasCourses = await dbContext.CurriculumCourses.AnyAsync(cc => cc.LevelId == levelId, ct);
        if (hasCourses)
            return Error.Validation("AcademicLevel.HasCourses", "Cannot delete a level that has curriculum courses mapped to it.");

        var hasEnrollments = await dbContext.Enrollments.AnyAsync(e => e.LevelId == levelId, ct);
        if (hasEnrollments)
            return Error.Validation("AcademicLevel.HasEnrollments", "Cannot delete a level with enrolled students.");

        var hasStudents = await dbContext.Students.AnyAsync(s => s.LevelId == levelId, ct);
        if (hasStudents)
            return Error.Validation("AcademicLevel.HasStudents", "Cannot delete a level with students attached.");

        dbContext.LevelSemesterConfigs.RemoveRange(level.Semesters);
        program.Levels.Remove(level);

        await programRepository.UpdateAsync(program, ct);
        await programRepository.SaveChangesAsync(ct);

        await LogActionAsync("DeleteLevel", "AcademicProgram", programId.ToString(), $"Deleted academic level: {level.Name}", ct);

        var updatedProgram = await programRepository.GetByIdAsync(programId, ct);
        return updatedProgram!.ToDto();
    }
}
