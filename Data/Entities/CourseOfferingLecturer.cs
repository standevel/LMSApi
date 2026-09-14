using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Join table: one CourseOffering → many lecturers with distinct roles.
/// Replaces the flat LecturerId + CoLecturersJson that used to live on CourseOffering.
/// </summary>
public sealed class CourseOfferingLecturer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourseOfferingId { get; set; }

    public Guid LecturerId { get; set; }

    public CourseLecturerRole Role { get; set; }

    [JsonIgnore]
    public CourseOffering CourseOffering { get; set; } = null!;

    [JsonIgnore]
    public AppUser Lecturer { get; set; } = null!;
}
