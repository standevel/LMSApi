namespace LMS.Api.Data.Entities;

public sealed class AssignmentSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid SubmitterId { get; set; }
    public Guid? GroupId { get; set; }
    public AssignmentSubmissionStatus Status { get; set; } = AssignmentSubmissionStatus.Draft;
    public DateTimeOffset? SubmittedAt { get; set; }
    public string SubmissionMetadataJson { get; set; } = "{}";
    public string DigitalReceipt { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public SubmissionGrade? Grade { get; set; }
}


public enum AssignmentSubmissionStatus
{
    Draft = 0,
    Submitted = 1,
    Late = 2,
    Graded = 3
}
