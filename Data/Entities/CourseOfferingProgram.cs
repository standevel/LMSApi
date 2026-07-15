using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Join table: one CourseOffering → many (Program, Level) pairs.
/// Replaces the flat ProgramId + LevelId that used to live on CourseOffering.
/// </summary>
public sealed class CourseOfferingProgram
{
    public Guid Id { get; set; }

    public Guid CourseOfferingId { get; set; }

    public Guid ProgramId { get; set; }

    public Guid LevelId { get; set; }

    [JsonIgnore]
    public CourseOffering CourseOffering { get; set; } = null!;

    [JsonIgnore]
    public AcademicProgram Program { get; set; } = null!;

    [JsonIgnore]
    public AcademicLevel Level { get; set; } = null!;
}
