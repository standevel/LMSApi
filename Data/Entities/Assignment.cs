namespace LMS.Api.Data.Entities;

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxPoints { get; set; } = 100m;
    public Guid CourseOfferingId { get; set; }
    public Guid? AssessmentCategoryId { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public DateTimeOffset? CutoffDate { get; set; }
    public string AllowedExtensions { get; set; } = "pdf,docx,zip";
    public int MaxFileSizeMb { get; set; } = 50;
    public bool IsGroupAssignment { get; set; }
    public int? MaxGroupSize { get; set; }
    public string ReleaseConditionsJson { get; set; } = "{}";
    public string TargetProgramIdsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public bool ReminderSent { get; set; } = false;

    public CourseOffering CourseOffering { get; set; } = null!;
    public ICollection<AssignmentSubmission> Submissions { get; set; } = [];
}
