using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public sealed class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid FacultyId { get; set; }
    public Faculty Faculty { get; set; } = null!;
    public DateOnly CreatedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly UpdatedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public ICollection<AcademicProgram> Programs { get; set; } = [];
}
