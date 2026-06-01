using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class DegreeAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? DegreeAuditTemplateId { get; set; }
    public DegreeAuditStatus Status { get; set; } = DegreeAuditStatus.InProgress;
    public int TotalCreditsRequired { get; set; }
    public int TotalCreditsEarned { get; set; }
    public int TotalCreditsInProgress { get; set; }
    public decimal CumulativeGpa { get; set; }
    public string? Summary { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public AppUser? Student { get; set; }
    [JsonIgnore]
    public AcademicProgram Program { get; set; } = null!;
    [JsonIgnore]
    public DegreeRequirement? Template { get; set; }
    public ICollection<DegreeAuditRequirement> Requirements { get; set; } = new List<DegreeAuditRequirement>();
    [JsonIgnore]
    public AppUser? Creator { get; set; }
}

public sealed class DegreeAuditRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DegreeAuditId { get; set; }
    public Guid RequirementId { get; set; }
    public RequirementCategory Category { get; set; }
    public string? RequirementName { get; set; }
    public int CreditsRequired { get; set; }
    public int CreditsEarned { get; set; }
    public bool IsCompleted { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DegreeAudit DegreeAudit { get; set; } = null!;
}

public enum RequirementCategory
{
    Core = 1,
    Elective = 2,
    GeneralEducation = 3,
    Major = 4,
    Minor = 5,
    Concentration = 6
}
