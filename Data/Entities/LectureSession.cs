using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Represents a specific instance of a lecture occurring on a particular date and time
/// </summary>
public sealed class LectureSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    public Guid? TimetableSlotId { get; set; }  // null for manually created sessions
    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Guid? VenueId { get; set; }
    public string? Notes { get; set; }
    public bool IsManuallyCreated { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public CourseOffering CourseOffering { get; set; } = null!;
    public LectureTimetableSlot? TimetableSlot { get; set; }
    public Subject? Venue { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public ICollection<LectureSessionLecturer> SessionLecturers { get; set; } = new List<LectureSessionLecturer>();
    public ICollection<SessionMaterial> Materials { get; set; } = new List<SessionMaterial>();
    public ICollection<SessionExternalLink> ExternalLinks { get; set; } = new List<SessionExternalLink>();
    public ICollection<SessionAttendance> Attendance { get; set; } = new List<SessionAttendance>();
}
