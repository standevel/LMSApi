using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Per-student time extension for accessibility/accommodation support.
/// </summary>
public class QuizTimeExtension
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;

    [Required]
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Required]
    [Column(TypeName = "int")]
    public int AdditionalMinutes { get; set; } // Additional minutes beyond the base time limit

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public string Status { get; set; } = "Active"; // Active, Expired, Revoked

    [Required]
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }
    public string? DocumentationUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
