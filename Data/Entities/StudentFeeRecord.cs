using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

/// <summary>
/// The computed fee bill for a student in a given academic session.
/// Total = sum of all assigned templates; adjusted when late fees are applied.
/// </summary>
public sealed class StudentFeeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid SessionId { get; set; }
    public AcademicSession Session { get; set; } = null!;

    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; } = 0;
    public decimal Balance => TotalAmount - AmountPaid;

    // Late fee tracking
    public bool LateFeeApplied { get; set; } = false;
    public decimal LateFeeTotal { get; set; } = 0;

    public FeeRecordStatus Status { get; set; } = FeeRecordStatus.Outstanding;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<FeePayment> Payments { get; set; } = new List<FeePayment>();
    public ICollection<LateFeeApplication> LateFeeApplications { get; set; } = new List<LateFeeApplication>();
}
