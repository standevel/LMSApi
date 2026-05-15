using System;

namespace LMS.Api.Data.Entities;

public sealed class ProgramCreditMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;

    // Credits required per academic level/year
    public int CreditsPerLevel { get; set; } = 30;

    // Maximum percentage of credits that can be transferred from external sources
    public decimal MaxTransferablePercentage { get; set; } = 50m;

    // Maximum total credits that can be transferred
    public int MaxTransferableCredits { get; set; } = 60;

    // Minimum credits that must be completed at LMS for graduation
    public int MinCreditsAtLMS { get; set; } = 60;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
