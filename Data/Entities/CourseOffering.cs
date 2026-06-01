using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class CourseOffering
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid LevelId { get; set; }
    public Guid AcademicSessionId { get; set; }
    public Guid? LecturerId { get; set; }
    public Semester Semester { get; set; }

    [JsonIgnore]
    public Course Course { get; set; } = null!;
    [JsonIgnore]
    public AcademicProgram Program { get; set; } = null!;
    [JsonIgnore]
    public AcademicLevel Level { get; set; } = null!;
    [JsonIgnore]
    public AcademicSession AcademicSession { get; set; } = null!;
    public AppUser? Lecturer { get; set; }
}
