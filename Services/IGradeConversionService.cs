namespace LMS.Api.Services;

public interface IGradeConversionService
{
    /// <summary>
    /// Converts a CGPA from the applicant's home country scale to LMS standard (4.0 scale).
    /// </summary>
    Task<GradeConversionResult> ConvertCGPAAsync(
        string countryCode,
        string? scaleName,
        decimal sourceCGPA,
        decimal scaleMax,
        decimal scaleMin,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the GPA scale conversion record for a country and scale name.
    /// </summary>
    Task<Data.Entities.GPAScaleConversion?> GetScaleConversionAsync(
        string countryCode,
        string scaleName,
        CancellationToken ct = default);
}

public sealed record GradeConversionResult(
    decimal ConvertedCGPA,
    string? ScaleName,
    decimal? OriginalScaleMax,
    decimal? OriginalScaleMin,
    bool IsEligible,
    string? Reason);
