using System;

namespace LMS.Api.Data.Entities;

public sealed class GPAScaleConversion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CountryCode { get; set; } = string.Empty;
    public string ScaleName { get; set; } = string.Empty;     // e.g., "British", "European ECTS", "Chinese"
    public decimal ScaleMax { get; set; }                      // Maximum scale value (e.g., 4.0, 5.0, 10.0)
    public decimal ScaleMin { get; set; } = 0m;               // Minimum scale value
    public decimal EquivalentCGPA { get; set; }                // Equivalent to LMS standard CGPA
    public decimal MinPassingScore { get; set; } = 50m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
