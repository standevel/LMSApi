using System;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class Waitlist
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    [Required]
    public Guid CourseOfferingId { get; set; }
    public CourseOffering? CourseOffering { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    [Required]
    public string Status { get; set; } = "Active";

    public int WaitlistRank { get; set; }
    public Guid CreatedById { get; set; }
    public Guid CreatedByUserId { get; set; }
}
