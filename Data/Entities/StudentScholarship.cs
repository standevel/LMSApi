using System;

namespace LMS.Api.Data.Entities;

public sealed class StudentScholarship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    
    public Guid ScholarshipId { get; set; }
    public Scholarship Scholarship { get; set; } = null!;
    
    public Guid SessionId { get; set; }
    public AcademicSession Session { get; set; } = null!;
    
    public decimal CalculatedAmount { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
