using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public sealed class BatchAutoRegistrationRequest
{
    public Guid AcademicSessionId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid? LevelId { get; set; }
    public Semester? TargetSemester { get; set; }
    public bool DryRun { get; set; } = false;
}

public sealed class StudentAutoRegistrationDetailDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? MatricNumber { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public int CoursesRegisteredCount { get; set; }
    public int TotalCredits { get; set; }
    public List<string> RegisteredCourses { get; set; } = new();
    public List<string> SkippedCourses { get; set; } = new();
    public string Status { get; set; } = "Success"; // "Success", "Skipped", "Failed"
    public string? Message { get; set; }
}

public sealed class BatchAutoRegistrationResultDto
{
    public int TotalStudentsEvaluated { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalCoursesRegistered { get; set; }
    public bool DryRun { get; set; }
    public List<StudentAutoRegistrationDetailDto> Details { get; set; } = new();
    public List<string> Logs { get; set; } = new();
}
