using System;
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

    public Course Course { get; set; } = null!;
    public AcademicProgram Program { get; set; } = null!;
    public AcademicLevel Level { get; set; } = null!;
    public AcademicSession AcademicSession { get; set; } = null!;
    public AppUser? Lecturer { get; set; }
}
