namespace LMS.Api.Data.Entities;

public sealed class AssignmentGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>JSON array of AppUser Ids (students) that belong to this group.</summary>
    public string MemberStudentIdsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Assignment Assignment { get; set; } = null!;
}
