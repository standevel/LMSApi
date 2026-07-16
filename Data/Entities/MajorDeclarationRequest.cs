using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class MajorDeclarationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid StudentId { get; set; }
    [JsonIgnore]
    public Student Student { get; set; } = null!;
    
    public Guid ParentProgramId { get; set; }
    [JsonIgnore]
    public AcademicProgram ParentProgram { get; set; } = null!;
    
    public Guid DeclaredProgramId { get; set; }
    [JsonIgnore]
    public AcademicProgram DeclaredProgram { get; set; } = null!;
    
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    
    public Guid? ApprovedById { get; set; }
    [JsonIgnore]
    public AppUser? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    public string? RejectionReason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
