namespace LMS.Api.Data.Entities;

public sealed class RegistrationVerification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid AcademicSessionId { get; set; }
    public Guid VerifiedByAdviserId { get; set; }
    public DateTime VerifiedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Verified";
    public string? Remarks { get; set; }
    public DateTime? UnlockedAtUtc { get; set; }
    public Guid? UnlockedById { get; set; }
    public string? UnlockReason { get; set; }

    public Student Student { get; set; } = null!;
    public AcademicSession AcademicSession { get; set; } = null!;
    public AppUser VerifiedByAdviser { get; set; } = null!;
    public AppUser? UnlockedBy { get; set; }
}
