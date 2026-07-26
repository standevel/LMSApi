using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public class HostelBlock
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    public HostelGenderType GenderType { get; set; } = HostelGenderType.Coed;

    public string? CampusLocation { get; set; }
    public int TotalFloors { get; set; } = 1;

    [MaxLength(150)]
    public string? WardenName { get; set; }
    [MaxLength(50)]
    public string? WardenPhone { get; set; }
    [MaxLength(150)]
    public string? WardenEmail { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public ICollection<HostelRoom> Rooms { get; set; } = new List<HostelRoom>();
}
