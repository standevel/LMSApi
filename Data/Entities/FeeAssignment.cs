using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Attaches a FeeTemplate to a specific scope target (Faculty/Program/Student).
/// A per-assignment due date can override the template's due date.
/// </summary>
public sealed class FeeAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FeeTemplateId { get; set; }
    public FeeTemplate FeeTemplate { get; set; } = null!;

    public FeeScope Scope { get; set; }

    // Scope target — exactly one of these should be set (or none for University scope)
    public Guid? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
    public Guid? ProgramId { get; set; }
    public AcademicProgram? Program { get; set; }
    public Guid? StudentId { get; set; }
    public AppUser? Student { get; set; }

    // Per-session override
    public Guid? SessionId { get; set; }
    public AcademicSession? Session { get; set; }

    // Optional: override the total amount charged for this scope target
    public decimal? AmountOverride { get; set; }

    // Optional: override the due date set on the template for this specific assignment
    public DateTime? DueDateOverride { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
