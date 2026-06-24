namespace LMS.Api.Data.Entities;

public sealed class CourseEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseOfferingId { get; set; }
    public string Status { get; set; } = "Registered";
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DroppedAtUtc { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }

    public AppUser Student { get; set; } = null!;
    public CourseOffering CourseOffering { get; set; } = null!;
    public AppUser CreatedBy { get; set; } = null!;
    public AppUser? UpdatedBy { get; set; }
}
