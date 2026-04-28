using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// System-wide grading configuration set by admin
/// </summary>
public sealed class SystemGradingConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Grading style: Weighted (CA1+CA2+CA3+Exam) or Unweighted (Simple Average)
    /// </summary>
    public GradingStyle DefaultGradingStyle { get; set; } = GradingStyle.Weighted;
    
    /// <summary>
    /// Minimum percentage of total that must come from exam (e.g., 60%)
    /// </summary>
    public decimal DefaultExamPercentage { get; set; } = 60m;
    
    /// <summary>
    /// Enable or disable the approval workflow system-wide
    /// </summary>
    public bool ApprovalWorkflowEnabled { get; set; } = true;
    
    /// <summary>
    /// Default CA1 weight (typically 15%)
    /// </summary>
    public decimal DefaultCA1Weight { get; set; } = 15m;
    
    /// <summary>
    /// Default CA2 weight (typically 15%)
    /// </summary>
    public decimal DefaultCA2Weight { get; set; } = 15m;
    
    /// <summary>
    /// Default CA3 weight (typically 10%)
    /// </summary>
    public decimal DefaultCA3Weight { get; set; } = 10m;
    
    /// <summary>
    /// Default Exam weight (typically 60%)
    /// </summary>
    public decimal DefaultExamWeight { get; set; } = 60m;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

public enum GradingStyle
{
    Weighted,
    Unweighted
}
