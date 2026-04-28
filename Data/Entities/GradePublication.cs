using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Tracks when grades are published and made visible to students
/// </summary>
public sealed class GradePublication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    
    /// <summary>
    /// When the grades were published
    /// </summary>
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who published the grades
    /// </summary>
    public Guid PublishedById { get; set; }
    
    /// <summary>
    /// Whether grades are visible to students
    /// </summary>
    public bool IsVisibleToStudents { get; set; }
    
    /// <summary>
    /// Whether the approval workflow was completed before publication
    /// </summary>
    public bool ApprovalWorkflowCompleted { get; set; }
    
    /// <summary>
    /// Publication notes or comments
    /// </summary>
    public string? PublicationNotes { get; set; }
    
    /// <summary>
    /// Academic session and semester when published
    /// </summary>
    public Guid AcademicSessionId { get; set; }
    public int Semester { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public CourseOffering CourseOffering { get; set; } = null!;
    public AppUser PublishedBy { get; set; } = null!;
}
