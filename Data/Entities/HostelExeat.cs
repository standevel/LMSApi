using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public class HostelExeat
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public Student? Student { get; set; }

    public Guid? HostelAllocationId { get; set; }

    [ForeignKey(nameof(HostelAllocationId))]
    public HostelAllocation? HostelAllocation { get; set; }

    [Required]
    public DateTime DepartureTime { get; set; }

    [Required]
    public DateTime ExpectedReturnTime { get; set; }

    public DateTime? ActualReturnTime { get; set; }

    [Required]
    [MaxLength(200)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    public string Reason { get; set; } = string.Empty;

    public ExeatStatus Status { get; set; } = ExeatStatus.Pending;

    public Guid? ApprovedByUserId { get; set; }

    [ForeignKey(nameof(ApprovedByUserId))]
    public AppUser? ApprovedByUser { get; set; }

    public string? WardenRemarks { get; set; }

    public bool ParentApproved { get; set; } = false;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}
