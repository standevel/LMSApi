using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.ProfileRequirements;

public sealed class ListProfileRequirementsEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<List<ProfileRequirementDto>>
{
    public override void Configure()
    {
        Get("admin/profile-requirements", "admin/admission-config/profile-campaigns");
        Roles("SuperAdmin", "Admin", "Registrar", "AdmissionOfficer", "AdmissionAdmin", "Dean", "HOD");
        Tags("Administration", "ProfileRequirements");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var requirements = await dbContext.ProfileCompletionRequirements
                .Include(r => r.AcademicSession)
                .Include(r => r.AcademicLevel)
                .Include(r => r.Faculty)
                .Include(r => r.AcademicProgram)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(ct);

            var dtos = requirements.Select(MapToDto).ToList();
            await SendSuccessAsync(dtos, ct);
        }
        catch (Exception)
        {
            // If the table doesn't exist yet or DB issue, safely return empty list
            await SendSuccessAsync([], ct);
        }
    }

    internal static ProfileRequirementDto MapToDto(ProfileCompletionRequirement r)
    {
        List<string> fields = [];
        if (!string.IsNullOrWhiteSpace(r.RequiredFieldsJson))
        {
            try
            {
                fields = JsonSerializer.Deserialize<List<string>>(r.RequiredFieldsJson) ?? [];
            }
            catch
            {
                fields = [];
            }
        }

        return new ProfileRequirementDto
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            AcademicSessionId = r.AcademicSessionId,
            AcademicSessionName = r.AcademicSession?.Name,
            AcademicLevelId = r.AcademicLevelId,
            AcademicLevelName = r.AcademicLevel?.Name,
            FacultyId = r.FacultyId,
            FacultyName = r.Faculty?.Name,
            AcademicProgramId = r.AcademicProgramId,
            AcademicProgramName = r.AcademicProgram?.Name,
            RequiredFields = fields,
            IsActive = r.IsActive,
            IsBlocking = r.IsBlocking,
            Deadline = r.Deadline,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };
    }
}

public sealed class CreateProfileRequirementEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateProfileRequirementRequest, ProfileRequirementDto>
{
    public override void Configure()
    {
        Post("admin/profile-requirements", "admin/admission-config/profile-campaigns");
        Roles("SuperAdmin", "Admin", "Registrar", "AdmissionOfficer", "AdmissionAdmin", "Dean", "HOD");
        Tags("Administration", "ProfileRequirements");
    }

    public override async Task HandleAsync(CreateProfileRequirementRequest req, CancellationToken ct)
    {
        var requirement = new ProfileCompletionRequirement
        {
            Title = string.IsNullOrWhiteSpace(req.Title) ? "Mandatory Student Profile Update" : req.Title.Trim(),
            Description = req.Description?.Trim() ?? string.Empty,
            AcademicSessionId = req.AcademicSessionId,
            AcademicLevelId = req.AcademicLevelId,
            FacultyId = req.FacultyId,
            AcademicProgramId = req.AcademicProgramId,
            RequiredFieldsJson = JsonSerializer.Serialize(req.RequiredFields ?? []),
            IsActive = req.IsActive,
            IsBlocking = req.IsBlocking,
            Deadline = req.Deadline,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.ProfileCompletionRequirements.Add(requirement);
        await dbContext.SaveChangesAsync(ct);

        // Re-query with navigation properties
        var created = await dbContext.ProfileCompletionRequirements
            .Include(r => r.AcademicSession)
            .Include(r => r.AcademicLevel)
            .Include(r => r.Faculty)
            .Include(r => r.AcademicProgram)
            .FirstOrDefaultAsync(r => r.Id == requirement.Id, ct) ?? requirement;

        await SendSuccessAsync(ListProfileRequirementsEndpoint.MapToDto(created), ct);
    }
}

public sealed class UpdateProfileRequirementEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateProfileRequirementRequest, ProfileRequirementDto>
{
    public override void Configure()
    {
        Patch("admin/profile-requirements/{Id}", "admin/admission-config/profile-campaigns/{Id}");
        Roles("SuperAdmin", "Admin", "Registrar", "AdmissionOfficer", "AdmissionAdmin", "Dean", "HOD");
        Tags("Administration", "ProfileRequirements");
    }

    public override async Task HandleAsync(UpdateProfileRequirementRequest req, CancellationToken ct)
    {
        var requirement = await dbContext.ProfileCompletionRequirements
            .Include(r => r.AcademicSession)
            .Include(r => r.AcademicLevel)
            .Include(r => r.Faculty)
            .Include(r => r.AcademicProgram)
            .FirstOrDefaultAsync(r => r.Id == req.Id, ct)
            ?? throw new KeyNotFoundException("Profile requirement not found");

        if (req.Title is not null) requirement.Title = req.Title.Trim();
        if (req.Description is not null) requirement.Description = req.Description.Trim();
        if (req.AcademicSessionId.HasValue) requirement.AcademicSessionId = req.AcademicSessionId == Guid.Empty ? null : req.AcademicSessionId;
        if (req.AcademicLevelId.HasValue) requirement.AcademicLevelId = req.AcademicLevelId == Guid.Empty ? null : req.AcademicLevelId;
        if (req.FacultyId.HasValue) requirement.FacultyId = req.FacultyId == Guid.Empty ? null : req.FacultyId;
        if (req.AcademicProgramId.HasValue) requirement.AcademicProgramId = req.AcademicProgramId == Guid.Empty ? null : req.AcademicProgramId;
        if (req.RequiredFields is not null) requirement.RequiredFieldsJson = JsonSerializer.Serialize(req.RequiredFields);
        if (req.IsActive.HasValue) requirement.IsActive = req.IsActive.Value;
        if (req.IsBlocking.HasValue) requirement.IsBlocking = req.IsBlocking.Value;
        if (req.Deadline.HasValue) requirement.Deadline = req.Deadline.Value;

        requirement.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(ListProfileRequirementsEndpoint.MapToDto(requirement), ct);
    }
}

public sealed class DeleteProfileRequirementEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteProfileRequirementRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("admin/profile-requirements/{Id}", "admin/admission-config/profile-campaigns/{Id}");
        Roles("SuperAdmin", "Admin", "Registrar", "AdmissionOfficer", "AdmissionAdmin", "Dean", "HOD");
        Tags("Administration", "ProfileRequirements");
    }

    public override async Task HandleAsync(DeleteProfileRequirementRequest req, CancellationToken ct)
    {
        var requirement = await dbContext.ProfileCompletionRequirements.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Profile requirement not found");

        dbContext.ProfileCompletionRequirements.Remove(requirement);
        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new EmptyResponse(), ct);
    }
}
