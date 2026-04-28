using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Assessment categories for a course offering (CA1, CA2, CA3, Exam, etc.)
/// </summary>
public sealed class AssessmentCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    
    /// <summary>
    /// Category type: CA1, CA2, CA3, Exam, or Custom
    /// </summary>
    public AssessmentCategoryType CategoryType { get; set; }
    
    /// <summary>
    /// Display name for the category
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;
    
    /// <summary>
    /// Weight percentage (e.g., 15 for 15%)
    /// </summary>
    public decimal Weight { get; set; }
    
    /// <summary>
    /// Maximum marks for assessments in this category
    /// </summary>
    public decimal MaxMarks { get; set; } = 100m;
    
    /// <summary>
    /// Whether this is an exam category (for exam percentage calculation)
    /// </summary>
    public bool IsExamCategory { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    public int DisplayOrder { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public CourseOffering CourseOffering { get; set; } = null!;
}

public enum AssessmentCategoryType
{
    CA1,
    CA2,
    CA3,
    Exam,
    Custom
}
