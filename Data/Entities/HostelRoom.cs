using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public class HostelRoom
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HostelBlockId { get; set; }

    [ForeignKey(nameof(HostelBlockId))]
    public HostelBlock? HostelBlock { get; set; }

    [Required]
    [MaxLength(50)]
    public string RoomNumber { get; set; } = string.Empty;

    public int FloorLevel { get; set; } = 1;

    [Required]
    [MaxLength(50)]
    public string RoomType { get; set; } = "Standard"; // Single, Double, 4-Bed, Suite, Deluxe

    public int Capacity { get; set; } = 2;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SemesterFeeRate { get; set; } = 0;

    public string AmenitiesJson { get; set; } = "[]"; // e.g. ["AC", "Ensuite Bath", "Study Desk"]

    public RoomStatus Status { get; set; } = RoomStatus.Available;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }

    public ICollection<HostelBed> Beds { get; set; } = new List<HostelBed>();
}
