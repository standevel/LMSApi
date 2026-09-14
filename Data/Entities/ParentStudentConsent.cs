using System;
using System.ComponentModel.DataAnnotations;

namespace LMS.Api.Data.Entities;

/// <summary>
/// A student's explicit consent decision for a given parent-access category.
/// Absence of a row means "use the institution default policy".
/// </summary>
public class ParentStudentConsent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    /// <summary>ParentAccessCategory value.</summary>
    [Required]
    public int Category { get; set; }

    /// <summary>True = student explicitly allows; False = student explicitly denies.</summary>
    [Required]
    public bool IsAllowed { get; set; }

    public DateTime SetAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? SetById { get; set; }
}
