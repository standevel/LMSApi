using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Approval workflow for grades at different levels
/// </summary>
public sealed class GradeApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    
    /// <summary>
    /// Approval level: Department (HOD), College (Dean), or Senate
    /// </summary>
    public ApprovalLevel Level { get; set; }
    
    /// <summary>
    /// Current status of the approval
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    
    /// <summary>
    /// User who approved/rejected
    /// </summary>
    public Guid? ApprovedById { get; set; }
    
    /// <summary>
    /// When the approval/rejection happened
    /// </summary>
    public DateTime? ApprovedAt { get; set; }
    
    /// <summary>
    /// Comments from the approver
    /// </summary>
    public string? Comments { get; set; }
    
    /// <summary>
    /// Whether this approval step is required (based on system config)
    /// </summary>
    public bool IsRequired { get; set; } = true;
    
    /// <summary>
    /// Order of approval (1=Department, 2=College, 3=Senate)
    /// </summary>
    public int ApprovalOrder { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public CourseOffering CourseOffering { get; set; } = null!;
    public AppUser? ApprovedBy { get; set; }
}

public enum ApprovalLevel
{
    Department,
    College,
    Senate
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}
