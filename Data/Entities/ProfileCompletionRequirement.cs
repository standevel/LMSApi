using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Admin-configurable requirement to enforce profile data completion for students matching criteria (level, program, session).
/// </summary>
public sealed class ProfileCompletionRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Mandatory Student Profile Update";
    public string Description { get; set; } = "Please complete the missing information in your student file.";

    // Targeting Criteria
    public Guid? AcademicSessionId { get; set; }
    public AcademicSession? AcademicSession { get; set; }

    public Guid? AcademicLevelId { get; set; }
    public AcademicLevel? AcademicLevel { get; set; }

    public Guid? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }

    public Guid? AcademicProgramId { get; set; }
    public AcademicProgram? AcademicProgram { get; set; }

    /// <summary>
    /// JSON array of field keys to enforce:
    /// e.g. ["gender", "dateOfBirth", "address", "emergencyContact", "oLevelResults"]
    /// </summary>
    public string RequiredFieldsJson { get; set; } = "[\"gender\",\"dateOfBirth\",\"address\",\"emergencyContact\"]";

    /// <summary>
    /// Master switch for this requirement
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// If true, students cannot dismiss the modal until filled
    /// </summary>
    public bool IsBlocking { get; set; } = true;

    /// <summary>
    /// Optional deadline date
    /// </summary>
    public DateTime? Deadline { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
