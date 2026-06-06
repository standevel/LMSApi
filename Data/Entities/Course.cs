using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; } // The program that hosts/owns this course
    public string Code { get; set; } = string.Empty; // e.g., CSC101
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CreditUnits { get; set; }
    public Guid? LevelId { get; set; }
    public Semester? Semester { get; set; }
    public int? LectureHours { get; set; }
    public int? PracticalHours { get; set; }
    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public AcademicProgram Program { get; set; } = null!;
    [JsonIgnore]
    public AcademicLevel? Level { get; set; }
    [JsonIgnore]
    public ICollection<CourseOffering> Offerings { get; set; } = [];
}
