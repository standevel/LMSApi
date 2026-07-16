using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AdmissionConfig;

public sealed class ListGradingScalesEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<IEnumerable<GradingScaleDto>>
{
    public override void Configure()
    {
        Get("admin/admission-config/grading-scales");
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var scales = await dbContext.GradingScales
            .OrderBy(s => s.CountryCode ?? "")
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        var response = scales.Select(s => new GradingScaleDto(
            s.Id, s.Name, s.CountryCode, s.QualificationType,
            string.IsNullOrEmpty(s.GradesJson)
                ? new List<GradingScaleGradeEntry>()
                : JsonSerializer.Deserialize<List<GradingScaleGradeEntry>>(s.GradesJson) ?? new List<GradingScaleGradeEntry>(),
            s.IsActive, s.CreatedAt, s.UpdatedAt));

        await SendSuccessAsync(response, ct);
    }
}

public record GradingScaleDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? QualificationType,
    List<GradingScaleGradeEntry> Grades,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateGradingScaleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateGradingScaleRequest, GradingScaleDto>
{
    public override void Configure()
    {
        Post("admin/admission-config/grading-scales");
        Tags("Administration");
    }

    public override async Task HandleAsync(CreateGradingScaleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            AddError(r => r.Name, "Scale Name is required.");
        }
        ThrowIfAnyErrors();

        var gradesSerialized = JsonSerializer.Serialize(req.Grades ?? new List<GradingScaleGradeEntry>());
        var scale = new GradingScale
        {
            Name = req.Name,
            CountryCode = req.CountryCode,
            QualificationType = req.QualificationType,
            GradesJson = gradesSerialized,
            IsActive = req.IsActive
        };
        dbContext.GradingScales.Add(scale);
        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new GradingScaleDto(
            scale.Id, scale.Name, scale.CountryCode, scale.QualificationType,
            req.Grades ?? new List<GradingScaleGradeEntry>(), scale.IsActive, scale.CreatedAt, scale.UpdatedAt), ct);
    }
}

public sealed class UpdateGradingScaleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateGradingScaleRequest, GradingScaleDto>
{
    public override void Configure()
    {
        Patch("admin/admission-config/grading-scales/{Id}");
        Tags("Administration");
    }

    public override async Task HandleAsync(UpdateGradingScaleRequest req, CancellationToken ct)
    {
        if (req.Name != null && string.IsNullOrWhiteSpace(req.Name))
        {
            AddError(r => r.Name, "Scale Name cannot be empty.");
        }
        ThrowIfAnyErrors();

        var scale = await dbContext.GradingScales.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Grading scale not found");

        scale.Name = req.Name ?? scale.Name;
        scale.CountryCode = req.CountryCode ?? scale.CountryCode;
        scale.QualificationType = req.QualificationType ?? scale.QualificationType;
        if (req.Grades != null)
        {
            scale.GradesJson = JsonSerializer.Serialize(req.Grades);
        }
        scale.IsActive = req.IsActive ?? scale.IsActive;
        scale.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        var deserializedGrades = string.IsNullOrEmpty(scale.GradesJson)
            ? new List<GradingScaleGradeEntry>()
            : JsonSerializer.Deserialize<List<GradingScaleGradeEntry>>(scale.GradesJson) ?? new List<GradingScaleGradeEntry>();

        await SendSuccessAsync(new GradingScaleDto(
            scale.Id, scale.Name, scale.CountryCode, scale.QualificationType,
            deserializedGrades, scale.IsActive, scale.CreatedAt, scale.UpdatedAt), ct);
    }
}

public sealed class DeleteGradingScaleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteGradingScaleRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("admin/admission-config/grading-scales/{Id}");
        Tags("Administration");
    }

    public override async Task HandleAsync(DeleteGradingScaleRequest req, CancellationToken ct)
    {
        var scale = await dbContext.GradingScales.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Grading scale not found");

        scale.IsActive = false;
        scale.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new EmptyResponse(), ct);
    }
}

public record CreateGradingScaleRequest(
    string Name,
    string? CountryCode,
    string? QualificationType,
    List<GradingScaleGradeEntry> Grades,
    bool IsActive = true);

public record UpdateGradingScaleRequest(
    Guid Id,
    string? Name,
    string? CountryCode,
    string? QualificationType,
    List<GradingScaleGradeEntry>? Grades,
    bool? IsActive);

public record DeleteGradingScaleRequest(Guid Id);
