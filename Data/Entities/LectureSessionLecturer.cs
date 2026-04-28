using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Join table entity for many-to-many relationship between LectureSession and AppUser (lecturers)
/// </summary>
public sealed class LectureSessionLecturer
{
    public Guid LectureSessionId { get; set; }
    public Guid LecturerId { get; set; }

    // Navigation properties
    public LectureSession LectureSession { get; set; } = null!;
    public AppUser Lecturer { get; set; } = null!;
}
