using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class DegreeRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public string? Name { get; set; } = string.Empty;
    public RequirementType Type { get; set; }
    public int CreditHoursRequired { get; set; }
    public decimal MinGpaRequired { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public AcademicProgram Program { get; set; } = null!;
    public ICollection<DegreeRequirementCourse> RequirementCourses { get; set; } = new List<DegreeRequirementCourse>();
}

public sealed class DegreeRequirementCourse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DegreeRequirementId { get; set; }
    public Guid CourseId { get; set; }
    public bool IsRequired { get; set; } = true;
    public int MinGrade { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DegreeRequirement DegreeRequirement { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
