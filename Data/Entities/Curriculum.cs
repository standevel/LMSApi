using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class Curriculum
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProgramId { get; set; }
    public Guid AdmissionSessionId { get; set; } // The session students were admitted under
    public string Name { get; set; } = string.Empty; // e.g., 2023/2024 Revised Curriculum
    public int MinCreditUnitsForGraduation { get; set; }
    public CurriculumStatus Status { get; set; } = CurriculumStatus.Draft;
    public Guid? ParentCurriculumId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public AcademicProgram Program { get; set; } = null!;
    public AcademicSession AdmissionSession { get; set; } = null!;
    public ICollection<CurriculumCourse> Courses { get; set; } = [];
}
