namespace LMS.Api.Data.Entities;

public sealed class SubmissionGrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid GraderId { get; set; }
    public decimal Score { get; set; }
    public string? FeedbackText { get; set; }
    public string? FeedbackMediaUrl { get; set; }
    public string RubricExecutionJson { get; set; } = "{}";
    public DateTimeOffset GradedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }

    public AssignmentSubmission Submission { get; set; } = null!;
}
