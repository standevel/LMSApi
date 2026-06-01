using System;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class ParentStudentLink
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ParentGuardianId { get; set; }
    public ParentGuardian? ParentGuardian { get; set; }

    [Required]
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    [Required]
    public string RelationshipType { get; set; } = string.Empty;

    [Required]
    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;
}
