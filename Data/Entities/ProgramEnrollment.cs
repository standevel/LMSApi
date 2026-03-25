using System;

namespace LMS.Api.Data.Entities;

public sealed class ProgramEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public Guid LevelId { get; set; }
    public Guid UserId { get; set; } // The student enrolled
    public Guid AcademicSessionId { get; set; }
    public Guid CurriculumId { get; set; }
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;

    public AcademicProgram Program { get; set; } = null!;
    public AcademicLevel Level { get; set; } = null!;
    public AppUser User { get; set; } = null!;
    public AcademicSession AcademicSession { get; set; } = null!;
    public Curriculum Curriculum { get; set; } = null!;
}
