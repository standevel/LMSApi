using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class DegreeAuditService : BaseService, IDegreeAuditService
{
    private readonly LmsDbContext _dbContext;

    public DegreeAuditService(LmsDbContext dbContext, IAuditService auditService) : base(auditService)
    {
        _dbContext = dbContext;
    }

    public async Task<ErrorOr<DegreeAuditDto>> GetDegreeAuditAsync(Guid auditId, CancellationToken ct = default)
    {
        var audit = await _dbContext.DegreeAudits
            .Include(x => x.Student)
            .Include(x => x.Program)
            .Include(x => x.Template)
            .Include(x => x.Requirements)
            .FirstOrDefaultAsync(x => x.Id == auditId, ct);

        if (audit == null)
            return DomainErrors.Reporting.DegreeAuditNotFound;

        return MapToDegreeAuditDto(audit);
    }

    public async Task<ErrorOr<DegreeAuditDto>> CreateDegreeAuditAsync(Guid studentId, CreateDegreeAuditRequest request, Guid createdBy, CancellationToken ct = default)
    {
        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == studentId, ct);
        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        var program = await _dbContext.Programs.FirstOrDefaultAsync(x => x.Id == request.ProgramId, ct);
        if (program == null)
            return DomainErrors.AcademicProgram.NotFound;

        // Check if there's an existing incomplete audit
        var existingAudit = await _dbContext.DegreeAudits
            .FirstOrDefaultAsync(x => x.StudentId == studentId && x.ProgramId == request.ProgramId && x.Status == DegreeAuditStatus.InProgress, ct);

        if (existingAudit != null)
            return Error.Conflict("DegreeAudit.Existing", "An incomplete degree audit already exists for this student and program");

        var audit = new DegreeAudit
        {
            StudentId = studentId,
            ProgramId = request.ProgramId,
            DegreeAuditTemplateId = request.TemplateId,
            Status = DegreeAuditStatus.InProgress,
            TotalCreditsRequired = program.DurationYears * 40, // Default 40 credits per year
            TotalCreditsEarned = 0,
            TotalCreditsInProgress = 0,
            CumulativeGpa = 0,
            GeneratedAt = DateTime.UtcNow,
            CreatedById = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        // If template is provided, copy requirements
        if (request.TemplateId.HasValue)
        {
            var templateRequirements = await _dbContext.DegreeRequirements
                .Where(r => r.ProgramId == request.ProgramId && r.IsActive)
                .ToListAsync(ct);

            foreach (var req in templateRequirements)
            {
                audit.Requirements.Add(new DegreeAuditRequirement
                {
                    RequirementId = req.Id,
                    Category = (RequirementCategory)req.Type,
                    RequirementName = req.Name,
                    CreditsRequired = req.CreditHoursRequired,
                    CreditsEarned = 0,
                    IsCompleted = false
                });
            }
        }

        _dbContext.DegreeAudits.Add(audit);
        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("CreateDegreeAudit", "DegreeAudit", audit.Id.ToString(),
            $"Created degree audit for student {studentId}", ct);

        return MapToDegreeAuditDto(audit);
    }

    public async Task<ErrorOr<List<DegreeAuditDto>>> GetStudentDegreeAuditsAsync(Guid studentId, CancellationToken ct = default)
    {
        var audits = await _dbContext.DegreeAudits
            .Include(x => x.Student)
            .Include(x => x.Program)
            .Include(x => x.Requirements)
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.GeneratedAt)
            .ToListAsync(ct);

        return audits.Select(MapToDegreeAuditDto).ToList();
    }

    public async Task<ErrorOr<DegreeRequirementDto>> GetDegreeRequirementAsync(Guid requirementId, CancellationToken ct = default)
    {
        var requirement = await _dbContext.DegreeRequirements
            .Include(x => x.Program)
            .Include(x => x.RequirementCourses)
                .ThenInclude(rc => rc.Course)
            .FirstOrDefaultAsync(x => x.Id == requirementId, ct);

        if (requirement == null)
            return DomainErrors.Reporting.DegreeRequirementNotFound;

        return MapToDegreeRequirementDto(requirement);
    }

    public async Task<ErrorOr<List<DegreeRequirementDto>>> GetProgramDegreeRequirementsAsync(Guid programId, CancellationToken ct = default)
    {
        var requirements = await _dbContext.DegreeRequirements
            .Include(x => x.RequirementCourses)
                .ThenInclude(rc => rc.Course)
            .Where(x => x.ProgramId == programId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        return requirements.Select(MapToDegreeRequirementDto).ToList();
    }

    public async Task<ErrorOr<DegreeRequirementDto>> CreateDegreeRequirementAsync(Guid programId, CreateDegreeRequirementRequest request, CancellationToken ct = default)
    {
        var program = await _dbContext.Programs.FirstOrDefaultAsync(x => x.Id == programId, ct);
        if (program == null)
            return DomainErrors.AcademicProgram.NotFound;

        var requirement = new DegreeRequirement
        {
            ProgramId = programId,
            Name = request.Name,
            Type = request.Type,
            CreditHoursRequired = request.CreditHoursRequired,
            MinGpaRequired = request.MinGpaRequired,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DegreeRequirements.Add(requirement);
        await _dbContext.SaveChangesAsync(ct);

        // Add courses if provided
        if (request.Courses != null && request.Courses.Any())
        {
            foreach (var courseReq in request.Courses)
            {
                requirement.RequirementCourses.Add(new DegreeRequirementCourse
                {
                    CourseId = courseReq.CourseId,
                    IsRequired = courseReq.IsRequired,
                    MinGrade = courseReq.MinGrade,
                    Remarks = courseReq.Remarks,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        await LogActionAsync("CreateDegreeRequirement", "DegreeRequirement", requirement.Id.ToString(),
            $"Created requirement '{request.Name}' for program {programId}", ct);

        return MapToDegreeRequirementDto(requirement);
    }

    public async Task<ErrorOr<DegreeRequirementDto>> UpdateDegreeRequirementAsync(Guid requirementId, UpdateDegreeRequirementRequest request, CancellationToken ct = default)
    {
        var requirement = await _dbContext.DegreeRequirements
            .Include(x => x.RequirementCourses)
            .FirstOrDefaultAsync(x => x.Id == requirementId, ct);

        if (requirement == null)
            return DomainErrors.Reporting.DegreeRequirementNotFound;

        requirement.Name = request.Name;
        requirement.Type = request.Type;
        requirement.CreditHoursRequired = request.CreditHoursRequired;
        requirement.MinGpaRequired = request.MinGpaRequired;
        requirement.Description = request.Description;
        requirement.DisplayOrder = request.DisplayOrder;
        requirement.IsActive = request.IsActive;
        requirement.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("UpdateDegreeRequirement", "DegreeRequirement", requirement.Id.ToString(),
            $"Updated requirement '{request.Name}'", ct);

        return MapToDegreeRequirementDto(requirement);
    }

    public async Task<ErrorOr<Deleted>> DeleteDegreeRequirementAsync(Guid requirementId, CancellationToken ct = default)
    {
        var requirement = await _dbContext.DegreeRequirements.FirstOrDefaultAsync(x => x.Id == requirementId, ct);
        if (requirement == null)
            return DomainErrors.Reporting.DegreeRequirementNotFound;

        _dbContext.DegreeRequirements.Remove(requirement);
        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("DeleteDegreeRequirement", "DegreeRequirement", requirement.Id.ToString(),
            $"Deleted requirement '{requirement.Name}'", ct);

        return Result.Deleted;
    }

    private DegreeAuditDto MapToDegreeAuditDto(DegreeAudit audit)
    {
        return new DegreeAuditDto(
            audit.Id,
            audit.StudentId,
            audit.Student != null
                ? (audit.Student.DisplayName ?? audit.Student.Email ?? string.Empty)
                : string.Empty,
            audit.ProgramId,
            audit.Program?.Name ?? "N/A",
            audit.Status,
            audit.TotalCreditsRequired,
            audit.TotalCreditsEarned,
            audit.TotalCreditsInProgress,
            audit.CumulativeGpa,
            audit.Summary,
            audit.Requirements.Select(r => new DegreeAuditRequirementDto(
                r.Id,
                r.Category.ToString(),
                r.Category,
                r.RequirementName,
                r.CreditsRequired,
                r.CreditsEarned,
                r.IsCompleted,
                r.Remarks)).ToList(),
            audit.GeneratedAt,
            audit.CompletedAt);
    }

    private DegreeRequirementDto MapToDegreeRequirementDto(DegreeRequirement requirement)
    {
        return new DegreeRequirementDto(
            requirement.Id,
            requirement.ProgramId,
            requirement.Program?.Name ?? "N/A",
            requirement.Name,
            requirement.Type,
            requirement.CreditHoursRequired,
            requirement.MinGpaRequired,
            requirement.Description,
            requirement.DisplayOrder,
            requirement.IsActive,
            requirement.RequirementCourses.Select(rc => new DegreeRequirementCourseDto(
                rc.Id,
                rc.CourseId,
                rc.Course?.Code ?? "N/A",
                rc.Course?.Title ?? "N/A",
                rc.IsRequired,
                rc.MinGrade,
                rc.Remarks)).ToList());
    }
}
