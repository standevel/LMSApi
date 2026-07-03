namespace LMS.Api.Data.Entities;

public sealed class AdvisingNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid AdviserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? FollowUpDateUtc { get; set; }
    public bool IsStaffOnly { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Student Student { get; set; } = null!;
    public AppUser Adviser { get; set; } = null!;
}
