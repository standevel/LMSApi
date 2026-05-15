using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public sealed class GradingScale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? QualificationType { get; set; }  // e.g., "A-Level", "IB", "HND"

    // Grade definitions stored as JSON for flexibility
    // Example: [{"Grade": "A", "MinScore": 75, "MaxScore": 100, "Points": 4.0}, ...]
    public string GradesJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation (for EF)
    public ICollection<DirectEntryGradeConfiguration> DirectEntryConfigurations { get; set; } = new List<DirectEntryGradeConfiguration>();
}

/// <summary>
/// Configurable grade-to-points mapping for direct entry qualifications.
/// Overrides the GradingScale for specific qualification types.
/// </summary>
public sealed class DirectEntryGradeConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GradingScaleId { get; set; }
    public GradingScale GradingScale { get; set; } = null!;

    // Qualification type this configuration applies to
    public string QualificationType { get; set; } = string.Empty;

    // Grade-to-points mapping stored as JSON
    // Example: [{"Grade": "A", "MinScore": 75, "MaxScore": 100, "Points": 3}]
    public string GradesJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
