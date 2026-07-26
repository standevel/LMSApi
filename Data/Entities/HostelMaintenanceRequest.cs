using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public class HostelMaintenanceRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HostelBlockId { get; set; }

    [ForeignKey(nameof(HostelBlockId))]
    public HostelBlock? HostelBlock { get; set; }

    public Guid? HostelRoomId { get; set; }

    [ForeignKey(nameof(HostelRoomId))]
    public HostelRoom? HostelRoom { get; set; }

    [Required]
    public Guid ReportedByUserId { get; set; }

    [ForeignKey(nameof(ReportedByUserId))]
    public AppUser? ReportedByUser { get; set; }

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = "General"; // Plumbing, Electrical, Carpentry, AC, Cleaning, Other

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;

    [MaxLength(150)]
    public string? AssignedTo { get; set; }

    public string? ResolutionNotes { get; set; }

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
