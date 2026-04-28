using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public sealed class Faculty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = "Faculty"; // e.g., College, Faculty, School
    public DateOnly CreatedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly UpdatedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public ICollection<Department> Departments { get; set; } = [];
    public ICollection<AcademicProgram> Programs { get; set; } = [];
}
