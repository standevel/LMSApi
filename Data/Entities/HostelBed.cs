using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public class HostelBed
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid HostelRoomId { get; set; }

    [ForeignKey(nameof(HostelRoomId))]
    public HostelRoom? HostelRoom { get; set; }

    [Required]
    [MaxLength(20)]
    public string BedLabel { get; set; } = "Bed A";

    public BedStatus Status { get; set; } = BedStatus.Vacant;

    public Guid? CurrentStudentId { get; set; }

    [ForeignKey(nameof(CurrentStudentId))]
    public Student? CurrentStudent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
