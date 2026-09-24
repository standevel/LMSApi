using System;

namespace LMS.Api.Data.Entities;

public sealed class SystemCafeteriaConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Master kill switch: when false, no self-service online top-up can be initiated.</summary>
    public bool EnableSelfServiceTopUp { get; set; } = true;

    /// <summary>When false, students cannot initiate self-service wallet top-ups.</summary>
    public bool AllowStudentSelfTopUp { get; set; } = true;

    /// <summary>When false, parents cannot initiate cafeteria wallet top-ups for their children.</summary>
    public bool AllowParentTopUp { get; set; } = true;

    /// <summary>Maximum single top-up allowed per transaction in NGN.</summary>
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    public decimal MaxSingleTopUpAmount { get; set; } = 100000m;

    /// <summary>Daily spend limit per student in NGN (0 = unlimited).</summary>
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    public decimal DailySpendLimit { get; set; } = 15000m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
