namespace LMS.Api.Contracts;

public sealed record AssignmentDto(
    Guid Id,
    string Title,
    string? Description,
    decimal MaxPoints,
    Guid CourseId,
    Guid? AssessmentCategoryId,
    DateTimeOffset DueDate,
    DateTimeOffset? CutoffDate,
    string AllowedExtensions,
    int MaxFileSizeMb,
    bool IsGroupAssignment,
    int? MaxGroupSize,
    string ReleaseConditionsJson,
    List<Guid> TargetProgramIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AssignmentSubmissionDto(
    Guid Id,
    Guid AssignmentId,
    Guid SubmitterId,
    Guid? GroupId,
    string Status,
    DateTimeOffset? SubmittedAt,
    string SubmissionMetadataJson,
    string DigitalReceipt,
    SubmissionGradeDto? Grade,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SubmissionGradeDto(
    Guid Id,
    Guid SubmissionId,
    Guid GraderId,
    decimal Score,
    string? FeedbackText,
    string? FeedbackMediaUrl,
    string RubricExecutionJson,
    DateTimeOffset GradedAt);

public class UpsertAssignmentRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxPoints { get; set; } = 100m;
    public Guid CourseId { get; set; }
    public Guid? AssessmentCategoryId { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public DateTimeOffset? CutoffDate { get; set; }
    public string AllowedExtensions { get; set; } = "pdf,docx,zip";
    public int MaxFileSizeMb { get; set; } = 50;
    public bool IsGroupAssignment { get; set; }
    public int? MaxGroupSize { get; set; }
    public string ReleaseConditionsJson { get; set; } = "{}";
    public List<Guid> TargetProgramIds { get; set; } = new();
}

public sealed class SubmitAssignmentRequest
{
    public Guid AssignmentId { get; set; }
    public Guid? SubmitterId { get; set; }
    public string SubmissionMetadataJson { get; set; } = "{}";
    public bool SaveAsDraft { get; set; }
}

public sealed class GradeSubmissionRequest
{
    public Guid SubmissionId { get; set; }
    public decimal Score { get; set; }
    public string? FeedbackText { get; set; }
    public string? FeedbackMediaUrl { get; set; }
    public string RubricExecutionJson { get; set; } = "{}";
}

// ── Assignment Groups ──────────────────────────────────────────────────────────

public sealed record AssignmentGroupDto(
    Guid Id,
    Guid AssignmentId,
    string Name,
    List<Guid> MemberStudentIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record EnrolledStudentDto(
    Guid UserId,
    string DisplayName,
    string? Email,
    string? StudentNumber);

public class CreateGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> StudentIds { get; set; } = new();
}

public class UpdateGroupRequest
{
    public string? Name { get; set; }
    public List<Guid>? StudentIds { get; set; }
}

public class AutoGroupRequest
{
    public int MaxPerGroup { get; set; } = 4;
}
