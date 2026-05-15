using System;

namespace LMS.Api.Data.Entities;

public sealed class ProgramPrerequisite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public AcademicProgram Program { get; set; } = null!;

    // Required subject (for O-Level/JAMB)
    public string RequiredSubjectCode { get; set; } = string.Empty;
    public string RequiredSubjectName { get; set; } = string.Empty;

    // Minimum grade required
    public string MinGrade { get; set; } = string.Empty;

    // Is this a core prerequisite (must have) vs elective (preferred)
    public bool IsCore { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
