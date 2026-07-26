using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public class HostelDevice
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
    public HostelDeviceType DeviceType { get; set; }

    [Required]
    [MaxLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string ModelNameNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string SerialNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MacAddressOrImei { get; set; }

    [MaxLength(500)]
    public string? ColorAndDescription { get; set; }

    [MaxLength(2000)]
    public string? ProofOfOwnershipUrl { get; set; }

    public HostelDeviceStatus Status { get; set; } = HostelDeviceStatus.PendingVerification;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public Guid? VerifiedByUserId { get; set; }

    [ForeignKey(nameof(VerifiedByUserId))]
    public AppUser? VerifiedByUser { get; set; }

    public DateTime? VerifiedAt { get; set; }

    [MaxLength(500)]
    public string? VerificationNotes { get; set; }

    public bool IsActive { get; set; } = true;
}
