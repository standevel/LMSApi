using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class AcademicProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // e.g., CS, EE
    public string? Description { get; set; }
    public string DegreeAwarded { get; set; } = string.Empty; // e.g., B.Sc., B.Eng.
    public Guid FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public ProgramType Type { get; set; } = ProgramType.Undergraduate;
    public int DurationYears { get; set; } = 4;
    public bool IsActive { get; set; } = true;

    // Admission Criteria
    public int MinJambScore { get; set; } = 150;
    public int MaxAdmissions { get; set; } = 100;
    public string RequiredJambSubjectsJson { get; set; } = "[]";
    public string RequiredOLevelSubjectsJson { get; set; } = "[]";

    public ICollection<AcademicLevel> Levels { get; set; } = [];
    public ICollection<ProgramEnrollment> Enrollments { get; set; } = [];
    public ICollection<Course> Courses { get; set; } = [];
}
