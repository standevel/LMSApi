using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public enum ScholarshipType
{
    JAMB,
    Sponsored,
    Merit
}

[Flags]
public enum ScholarshipCoverageFlags
{
    None = 0,
    Tuition = 1 << 0,
    Accommodation = 1 << 1,
    Feeding = 1 << 2,
    Full = Tuition | Accommodation | Feeding
}

public sealed class Scholarship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public ScholarshipType Type { get; set; } = ScholarshipType.JAMB;
    public ScholarshipCoverageFlags CoverageFlags { get; set; } = ScholarshipCoverageFlags.Tuition;
    
    public decimal PercentageCovered { get; set; } = 100;
    
    public Guid? SponsorOrganizationId { get; set; }
    public SponsorOrganization? SponsorOrganization { get; set; }
    
    // For JAMB type
    public int? MinJambScore { get; set; }
    public int? MaxJambScore { get; set; }
    
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ICollection<StudentScholarship> StudentScholarships { get; set; } = new List<StudentScholarship>();
}
