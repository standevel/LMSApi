using System;

namespace LMS.Api.Data.Entities;

public sealed class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // e.g., CSC101
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CreditUnits { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CourseOffering> Offerings { get; set; } = [];
}
