using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Represents one offering of a course for a given academic session and semester.
/// A single offering can be consumed by multiple programs (via CourseOfferingProgram)
/// and taught by multiple lecturers with different roles (via CourseOfferingLecturer).
/// </summary>
public sealed class CourseOffering
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public Guid AcademicSessionId { get; set; }

    public Semester Semester { get; set; }

    public Guid? CurriculumId { get; set; }

    [JsonIgnore]
    public Course Course { get; set; } = null!;

    [JsonIgnore]
    public AcademicSession AcademicSession { get; set; } = null!;

    [JsonIgnore]
    public Curriculum? Curriculum { get; set; }

    /// <summary>Programs (and associated levels) that consume this offering.</summary>
    [JsonIgnore]
    public ICollection<CourseOfferingProgram> Programs { get; set; } = new List<CourseOfferingProgram>();

    /// <summary>Lecturers assigned to this offering, with Main or CoLecturer roles.</summary>
    [JsonIgnore]
    public ICollection<CourseOfferingLecturer> Lecturers { get; set; } = new List<CourseOfferingLecturer>();
}
