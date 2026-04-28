using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Individual assessment (test, assignment, exam) within a category
/// </summary>
public sealed class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    public Guid AssessmentCategoryId { get; set; }
    
    /// <summary>
    /// Title of the assessment
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Maximum marks for this assessment
    /// </summary>
    public decimal MaxMarks { get; set; } = 100m;
    
    /// <summary>
    /// Date when the assessment was conducted
    /// </summary>
    public DateTime? AssessmentDate { get; set; }
    
    /// <summary>
    /// Due date for assignment submissions
    /// </summary>
    public DateTime? DueDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public CourseOffering CourseOffering { get; set; } = null!;
    public AssessmentCategory AssessmentCategory { get; set; } = null!;
    public ICollection<Grade> Grades { get; set; } = [];
}
