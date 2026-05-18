using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AdmissionConfig;

public sealed class ListGradingScalesEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<IEnumerable<GradingScaleDto>>
{
    public override void Configure()
    {
        Get("/api/admin/admission-config/grading-scales");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var scales = await dbContext.GradingScales
            .OrderBy(s => s.CountryCode ?? "")
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

        var response = scales.Select(s => new GradingScaleDto(
            s.Id, s.Name, s.CountryCode, s.QualificationType,
            s.GradesJson, s.IsActive, s.CreatedAt, s.UpdatedAt));

        await SendSuccessAsync(response, ct);
    }
}

public record GradingScaleDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? QualificationType,
    string GradesJson,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateGradingScaleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateGradingScaleRequest, GradingScaleDto>
{
    public override void Configure()
    {
        Post("/api/admin/admission-config/grading-scales");
    }

    public override async Task HandleAsync(CreateGradingScaleRequest req, CancellationToken ct)
    {
        var scale = new GradingScale
        {
            Name = req.Name,
            CountryCode = req.CountryCode,
            QualificationType = req.QualificationType,
            GradesJson = req.GradesJson,
            IsActive = req.IsActive
        };
        dbContext.GradingScales.Add(scale);
        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new GradingScaleDto(
            scale.Id, scale.Name, scale.CountryCode, scale.QualificationType,
            scale.GradesJson, scale.IsActive, scale.CreatedAt, scale.UpdatedAt), ct);
    }
}

public sealed class UpdateGradingScaleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateGradingScaleRequest, GradingScaleDto>
{
    public override void Configure()
    {
        Patch("/api/admin/admission-config/grading-scales/{Id}");
    }

    public override async Task HandleAsync(UpdateGradingScaleRequest req, CancellationToken ct)
    {
        var scale = await dbContext.GradingScales.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("Grading scale not found");

        scale.Name = req.Name ?? scale.Name;
        scale.CountryCode = req.CountryCode ?? scale.CountryCode;
        scale.QualificationType = req.QualificationType ?? scale.QualificationType;
        scale.GradesJson = req.GradesJson ?? scale.GradesJson;
        scale.IsActive = req.IsActive ?? scale.IsActive;
        scale.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new GradingScaleDto(
            scale.Id, scale.Name, scale.CountryCode, scale.QualificationType,
            scale.GradesJson, scale.IsActive, scale.CreatedAt, scale.UpdatedAt), ct);
    }
}

public sealed class DeleteGradingScaleEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteGradingScaleRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("/api/admin/admission-config/grading-scales/{Id}");
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
    string GradesJson,
    bool IsActive = true);

public record UpdateGradingScaleRequest(
    Guid Id,
    string? Name,
    string? CountryCode,
    string? QualificationType,
    string? GradesJson,
    bool? IsActive);

public record DeleteGradingScaleRequest(Guid Id);
