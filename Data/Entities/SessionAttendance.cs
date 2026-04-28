using System;

namespace LMS.Api.Data.Entities;

public sealed class SessionAttendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LectureSessionId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsPresent { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Guid RecordedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid? ModifiedBy { get; set; }

    // Navigation properties
    public LectureSession LectureSession { get; set; } = null!;
    public AppUser Student { get; set; } = null!;
    public AppUser RecordedByUser { get; set; } = null!;
    public AppUser? ModifiedByUser { get; set; }
}
