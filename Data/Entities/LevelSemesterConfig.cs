using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class LevelSemesterConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LevelId { get; set; }
    public Semester Semester { get; set; }
    public int MaxCreditLoad { get; set; } = 24;
    public bool IsActive { get; set; } = true;

    public AcademicLevel Level { get; set; } = null!;
}
