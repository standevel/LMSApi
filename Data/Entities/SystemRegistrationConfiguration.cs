using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Global configuration for course registration settings
/// </summary>
public sealed class SystemRegistrationConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Course registration strategy: Single or Bulk
    /// </summary>
    public string Strategy { get; set; } = "Single";
    
    /// <summary>
    /// Whether to dynamically calculate and enforce minimum credit hours from the curriculum
    /// </summary>
    public bool EnforceMinCredits { get; set; } = true;

    /// <summary>
    /// Global template for student matric numbers (defaults to "WU/{YY}/{PROGRAM}/{SEQ}")
    /// </summary>
    public string MatricNumberFormat { get; set; } = "WU/{YY}/{PROGRAM}/{SEQ}";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
