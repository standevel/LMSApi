using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class CoursePrerequisite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid PrerequisiteCourseId { get; set; }
    public PrerequisiteType Type { get; set; } = PrerequisiteType.HardPrerequisite;

    public Course Course { get; set; } = null!;
    public Course PrerequisiteCourse { get; set; } = null!;
}
