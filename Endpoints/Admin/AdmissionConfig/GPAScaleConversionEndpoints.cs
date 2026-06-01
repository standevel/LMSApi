using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin.AdmissionConfig;

public sealed class ListGPAScaleConversionsEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<IEnumerable<GPAScaleConversionDto>>
{
    public override void Configure()
    {
        Get("admin/admission-config/gpa-scale-conversions");
        Tags("Administration");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var conversions = await dbContext.GPAScaleConversions
            .OrderBy(c => c.CountryCode)
            .ThenBy(c => c.ScaleName)
            .ToListAsync(ct);

        var response = conversions.Select(c => new GPAScaleConversionDto(
            c.Id, c.CountryCode, c.ScaleName, c.ScaleMax, c.ScaleMin,
            c.EquivalentCGPA, c.MinPassingScore, c.IsActive, c.CreatedAt));

        await SendSuccessAsync(response, ct);
    }
}

public record GPAScaleConversionDto(
    Guid Id,
    string CountryCode,
    string ScaleName,
    decimal ScaleMax,
    decimal ScaleMin,
    decimal EquivalentCGPA,
    decimal MinPassingScore,
    bool IsActive,
    DateTime CreatedAt);

public sealed class CreateGPAScaleConversionEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<CreateGPAScaleConversionRequest, GPAScaleConversionDto>
{
    public override void Configure()
    {
        Post("admin/admission-config/gpa-scale-conversions");
        Tags("Administration");
    }

    public override async Task HandleAsync(CreateGPAScaleConversionRequest req, CancellationToken ct)
    {
        var conversion = new GPAScaleConversion
        {
            CountryCode = req.CountryCode,
            ScaleName = req.ScaleName,
            ScaleMax = req.ScaleMax,
            ScaleMin = req.ScaleMin,
            EquivalentCGPA = req.EquivalentCGPA,
            MinPassingScore = req.MinPassingScore,
            IsActive = req.IsActive
        };
        dbContext.GPAScaleConversions.Add(conversion);
        await dbContext.SaveChangesAsync(ct);

        var dto = new GPAScaleConversionDto(
            conversion.Id, conversion.CountryCode, conversion.ScaleName,
            conversion.ScaleMax, conversion.ScaleMin, conversion.EquivalentCGPA,
            conversion.MinPassingScore, conversion.IsActive, conversion.CreatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class UpdateGPAScaleConversionEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateGPAScaleConversionRequest, GPAScaleConversionDto>
{
    public override void Configure()
    {
        Patch("admin/admission-config/gpa-scale-conversions/{Id}");
        Tags("Administration");
    }

    public override async Task HandleAsync(UpdateGPAScaleConversionRequest req, CancellationToken ct)
    {
        var conversion = await dbContext.GPAScaleConversions.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("GPA scale conversion not found");

        conversion.CountryCode = req.CountryCode ?? conversion.CountryCode;
        conversion.ScaleName = req.ScaleName ?? conversion.ScaleName;
        conversion.ScaleMax = req.ScaleMax ?? conversion.ScaleMax;
        conversion.ScaleMin = req.ScaleMin ?? conversion.ScaleMin;
        conversion.EquivalentCGPA = req.EquivalentCGPA ?? conversion.EquivalentCGPA;
        conversion.MinPassingScore = req.MinPassingScore ?? conversion.MinPassingScore;
        conversion.IsActive = req.IsActive ?? conversion.IsActive;

        await dbContext.SaveChangesAsync(ct);

        var dto = new GPAScaleConversionDto(
            conversion.Id, conversion.CountryCode, conversion.ScaleName,
            conversion.ScaleMax, conversion.ScaleMin, conversion.EquivalentCGPA,
            conversion.MinPassingScore, conversion.IsActive, conversion.CreatedAt);
        await SendSuccessAsync(dto, ct);
    }
}

public sealed class DeleteGPAScaleConversionEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<DeleteGPAScaleConversionRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("admin/admission-config/gpa-scale-conversions/{Id}");
        Tags("Administration");
    }

    public override async Task HandleAsync(DeleteGPAScaleConversionRequest req, CancellationToken ct)
    {
        var conversion = await dbContext.GPAScaleConversions.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException("GPA scale conversion not found");

        conversion.IsActive = false;
        await dbContext.SaveChangesAsync(ct);
        await SendSuccessAsync(new EmptyResponse(), ct);
    }
}

public record CreateGPAScaleConversionRequest(
    string CountryCode,
    string ScaleName,
    decimal ScaleMax,
    decimal ScaleMin,
    decimal EquivalentCGPA,
    decimal MinPassingScore,
    bool IsActive = true);

public record UpdateGPAScaleConversionRequest(
    Guid Id,
    string? CountryCode,
    string? ScaleName,
    decimal? ScaleMax,
    decimal? ScaleMin,
    decimal? EquivalentCGPA,
    decimal? MinPassingScore,
    bool? IsActive);

public record DeleteGPAScaleConversionRequest(Guid Id);
