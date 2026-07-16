using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class AcademicProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // e.g., CS, EE
    public string? Description { get; set; }
    public string DegreeAwarded { get; set; } = string.Empty; // e.g., B.Sc., B.Eng.
    public Guid DepartmentId { get; set; }
    [JsonIgnore]
    public Department Department { get; set; } = null!;
    public ProgramType Type { get; set; } = ProgramType.Undergraduate;
    public int DurationYears { get; set; } = 4;
    public bool IsActive { get; set; } = true;

    // Specialization / Sub-major options
    public Guid? ParentProgramId { get; set; }
    [JsonIgnore]
    public AcademicProgram? ParentProgram { get; set; }
    [JsonIgnore]
    public ICollection<AcademicProgram> ChildPrograms { get; set; } = [];
    public int? SpecializationStartYear { get; set; }


    // Admission Criteria
    public int MinJambScore { get; set; } = 150;
    public int MaxAdmissions { get; set; } = 100;
    public string RequiredJambSubjectsJson { get; set; } = "[]";
    public string RequiredOLevelSubjectsJson { get; set; } = "[]";

    // Computed navigation to College (Faculty) via Department
    public Faculty? Faculty => Department?.Faculty;

    [JsonIgnore]
    public ICollection<AcademicLevel> Levels { get; set; } = [];
    [JsonIgnore]
    public ICollection<ProgramEnrollment> Enrollments { get; set; } = [];
    [JsonIgnore]
    public ICollection<Course> Courses { get; set; } = [];
}
