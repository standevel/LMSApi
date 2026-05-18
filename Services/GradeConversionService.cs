using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class GradeConversionService(LmsDbContext dbContext) : IGradeConversionService
{
    public async Task<GradeConversionResult> ConvertCGPAAsync(
        string countryCode,
        string? scaleName,
        decimal sourceCGPA,
        decimal scaleMax,
        decimal scaleMin,
        CancellationToken ct = default)
    {
        // First, try to find a matching conversion record
        var conversion = await GetScaleConversionAsync(countryCode, scaleName, ct);

        if (conversion != null)
        {
            // Use the pre-configured equivalent CGPA from the conversion table
            var convertedCGPA = conversion.EquivalentCGPA;

            // If the source CGPA is within the scale range, apply proportional mapping
            if (sourceCGPA >= conversion.ScaleMin && sourceCGPA <= conversion.ScaleMax && conversion.ScaleMax > conversion.ScaleMin)
            {
                var proportion = (sourceCGPA - conversion.ScaleMin) / (conversion.ScaleMax - conversion.ScaleMin);
                // Map proportion to a 4.0 scale
                convertedCGPA = proportion * 4.0m;
            }
            else if (sourceCGPA > conversion.ScaleMax)
            {
                convertedCGPA = 4.0m; // Cap at maximum
            }
            else if (sourceCGPA < conversion.ScaleMin)
            {
                convertedCGPA = 0m; // Below minimum
            }

            return new GradeConversionResult(
                Math.Round(convertedCGPA, 2),
                conversion.ScaleName,
                conversion.ScaleMax,
                conversion.ScaleMin,
                sourceCGPA >= conversion.MinPassingScore,
                sourceCGPA < conversion.MinPassingScore ? $"Score below minimum passing threshold of {conversion.MinPassingScore}." : null);
        }

        // Fallback: proportional mapping to 4.0 scale
        if (scaleMax > scaleMin)
        {
            var proportion = (sourceCGPA - scaleMin) / (scaleMax - scaleMin);
            var convertedCGPA = proportion * 4.0m;

            return new GradeConversionResult(
                Math.Round(convertedCGPA, 2),
                $"Auto-converted from {scaleMax}-point scale",
                scaleMax,
                scaleMin,
                true,
                null);
        }

        // Default: assume 5.0 scale if no info
        if (scaleMax <= 0)
        {
            var convertedCGPA = (sourceCGPA / 5.0m) * 4.0m;
            return new GradeConversionResult(
                Math.Round(convertedCGPA, 2),
                "Assumed 5.0 scale (no conversion data available)",
                5.0m,
                0m,
                sourceCGPA >= 2.5m,
                sourceCGPA < 2.5m ? "Score below minimum 2.5/5.0 threshold." : null);
        }

        return new GradeConversionResult(0, "Unknown scale", scaleMax, scaleMin, false, "Unable to convert CGPA — invalid scale.");
    }

    public async Task<GPAScaleConversion?> GetScaleConversionAsync(
        string countryCode,
        string scaleName,
        CancellationToken ct = default)
    {
        // Try exact match first
        var result = await dbContext.GPAScaleConversions
            .FirstOrDefaultAsync(s => s.CountryCode == countryCode
                && s.ScaleName == scaleName
                && s.IsActive, ct);
        if (result != null) return result;

        // Try country-agnostic (CountryCode = null or empty)
        return await dbContext.GPAScaleConversions
            .FirstOrDefaultAsync(s => (s.CountryCode == null || s.CountryCode == string.Empty)
                && s.ScaleName == scaleName
                && s.IsActive, ct);
    }
}
