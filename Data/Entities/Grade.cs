using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Student's grade for a specific assessment
/// </summary>
public sealed class Grade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssessmentId { get; set; }
    public Guid StudentId { get; set; }
    
    /// <summary>
    /// Marks obtained by the student (can exceed MaxMarks for bonus)
    /// </summary>
    public decimal MarksObtained { get; set; }
    
    /// <summary>
    /// Whether this grade is locked (cannot be edited after approval)
    /// </summary>
    public bool IsLocked { get; set; }
    
    /// <summary>
    /// Remarks or comments on the grade
    /// </summary>
    public string? Remarks { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    
    public Assessment Assessment { get; set; } = null!;
    public AppUser Student { get; set; } = null!;
    public AppUser? CreatedBy { get; set; }
    public AppUser? UpdatedBy { get; set; }
}
