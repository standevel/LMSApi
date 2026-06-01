using System;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class CourseSwapRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    [Required]
    public Guid CourseOfferingToDropId { get; set; }
    public CourseOffering? CourseOfferingToDrop { get; set; }

    [Required]
    public Guid CourseOfferingToAddId { get; set; }
    public CourseOffering? CourseOfferingToAdd { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public Guid? ProcessedById { get; set; }
    public AppUser? ProcessedBy { get; set; }
    public string? RejectionReason { get; set; }

    [Required]
    public string Status { get; set; } = "Pending";

    public Guid CreatedById { get; set; }
    public Guid CreatedByUserId { get; set; }
}
