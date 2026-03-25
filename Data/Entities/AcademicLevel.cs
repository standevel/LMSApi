using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public sealed class AcademicLevel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., 100 Level, Year 1
    public int Order { get; set; } // For sorting levels

    public AcademicProgram Program { get; set; } = null!;
    public ICollection<ProgramEnrollment> Enrollments { get; set; } = [];
    public ICollection<Course> Courses { get; set; } = [];
    public ICollection<LevelSemesterConfig> Semesters { get; set; } = [];
}
