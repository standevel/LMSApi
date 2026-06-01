using System;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public AcademicProgram Program { get; set; } = null!;
    [JsonIgnore]
    public AcademicLevel Level { get; set; } = null!;
    public AppUser User { get; set; } = null!;
    [JsonIgnore]
    public AcademicSession AcademicSession { get; set; } = null!;
    [JsonIgnore]
    public Curriculum Curriculum { get; set; } = null!;
}
