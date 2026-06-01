using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class AcademicStanding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid AcademicSessionId { get; set; }
    public AcademicStandingType StandingType { get; set; }
    public decimal CumulativeGpa { get; set; }
    public int TotalCreditsAttempted { get; set; }
    public int TotalCreditsEarned { get; set; }
    public string? Remarks { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryDate { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public AppUser? Student { get; set; }
    [JsonIgnore]
    public AcademicSession AcademicSession { get; set; } = null!;
    [JsonIgnore]
    public AppUser? Creator { get; set; }
}

public enum AcademicStandingType
{
    GoodStanding = 1,
    Probation = 2,
    Suspension = 3,
    Expulsion = 4,
    HonorRoll = 5,
    DeanList = 6
}
