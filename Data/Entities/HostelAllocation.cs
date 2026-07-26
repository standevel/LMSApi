using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public class HostelAllocation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public Student? Student { get; set; }

    [Required]
    public Guid AcademicSessionId { get; set; }

    [ForeignKey(nameof(AcademicSessionId))]
    public AcademicSession? AcademicSession { get; set; }

    public Guid? HostelBedId { get; set; }

    [ForeignKey(nameof(HostelBedId))]
    public HostelBed? HostelBed { get; set; }

    public Guid? PreferredBlockId { get; set; }

    [ForeignKey(nameof(PreferredBlockId))]
    public HostelBlock? PreferredBlock { get; set; }

    [MaxLength(50)]
    public string? PreferredRoomType { get; set; }

    public string? SpecialNeeds { get; set; }

    public AllocationStatus Status { get; set; } = AllocationStatus.Pending;

    public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;
    public DateTime? AllocatedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }

    public Guid? FeeRecordId { get; set; }

    [ForeignKey(nameof(FeeRecordId))]
    public StudentFeeRecord? FeeRecord { get; set; }

    public string? Notes { get; set; }
}
