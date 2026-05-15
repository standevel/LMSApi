using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class CreditTransferRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;

    // Source country (null = applies to all)
    public string? SourceCountryCode { get; set; }

    // Credits awarded per year of study at source institution
    public decimal CreditsPerYear { get; set; } = 15m;

    // Maximum percentage of degree requirements that can be transferred
    public decimal MaxTransferablePercentage { get; set; } = 50m;

    // Maximum total credits that can be transferred
    public int MaxTransferableCredits { get; set; } = 60;

    // Minimum CGPA required for credit transfer consideration
    public decimal MinCGPA { get; set; } = 2.5m;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
