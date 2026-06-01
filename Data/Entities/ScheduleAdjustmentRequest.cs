using System;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class ScheduleAdjustmentRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;

    public DateTime RequestedDate { get; set; }
    
    [Required]
    public string DesiredSlotDetails { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = "Pending";

    public Guid CreatedById { get; set; }
    public Guid CreatedByUserId { get; set; }
}
