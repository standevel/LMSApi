using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Tracks a late-fee surcharge applied to a student's fee record for a specific template.
/// This creates a full audit trail: who was charged, when, how much, and why.
/// </summary>
public sealed class LateFeeApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentFeeRecordId { get; set; }
    public StudentFeeRecord StudentFeeRecord { get; set; } = null!;

    // Which template's late fee rule triggered this
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;

    public decimal AmountCharged { get; set; }
    public LateFeeType FeeType { get; set; }  // Fixed or Percentage (recorded at time of application)
    public decimal BaseRateUsed { get; set; } // The configured LateFeeAmount at time of application
    public DateTime EffectiveDueDate { get; set; } // The due date that was breached
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public string AppliedBy { get; set; } = "System"; // "System" for automated / admin email for manual
}
