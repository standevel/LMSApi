using System;

namespace LMS.Api.Data.Entities;

public sealed class CourseEquivalency
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Source institution info
    public string SourceInstitution { get; set; } = string.Empty;
    public string SourceCourseCode { get; set; } = string.Empty;
    public string SourceCourseName { get; set; } = string.Empty;
    public decimal SourceCredits { get; set; }

    // Target mapping
    public Guid? TargetCourseId { get; set; }
    public Course? TargetCourse { get; set; }
    public decimal TargetCredits { get; set; }

    // Mapping metadata
    public string? Description { get; set; }
    public string? MappingNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
