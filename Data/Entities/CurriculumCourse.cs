using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class CurriculumCourse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CurriculumId { get; set; }
    public Guid LevelId { get; set; }
    public Guid CourseId { get; set; }
    public Semester Semester { get; set; }
    public CourseCategory Category { get; set; }
    public int CreditUnits { get; set; }

    public Curriculum Curriculum { get; set; } = null!;
    public AcademicLevel Level { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
