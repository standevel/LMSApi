namespace LMS.Api.Data.Entities;

public sealed class CourseAdviserAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid AdviserId { get; set; }
    public Guid AssignedById { get; set; }
    public string Status { get; set; } = "Active";
    public string Source { get; set; } = "Manual";
    public string? Note { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }

    public Student Student { get; set; } = null!;
    public AppUser Adviser { get; set; } = null!;
    public AppUser AssignedBy { get; set; } = null!;
}
