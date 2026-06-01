using System;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class PrerequisiteOverride
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    [Required]
    public Guid CourseOfferingId { get; set; }
    public CourseOffering? CourseOffering { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public AppUser? ApprovedBy { get; set; }
    public string? ApprovedByName { get; set; }
    public string? RejectionReason { get; set; }

    public Guid CreatedById { get; set; }
    public Guid CreatedByUserId { get; set; }
}