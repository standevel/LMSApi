using System;
using System.Collections.Generic;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class FeeTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid FeeCategoryId { get; set; }
    public FeeCategory Category { get; set; } = null!;
    public FeeScope Scope { get; set; } = FeeScope.University;

    // Nullable session = recurring every session; non-null = specific session only
    public Guid? SessionId { get; set; }
    public AcademicSession? Session { get; set; }

    // Optional scope filters — set when Scope = Faculty or Program
    public Guid? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
    public Guid? ProgramId { get; set; }
    public AcademicProgram? Program { get; set; }

    // Late fee configuration — applies per template by default
    public DateTime? DueDate { get; set; }
    public LateFeeType LateFeeType { get; set; } = LateFeeType.Fixed;
    public decimal LateFeeAmount { get; set; } = 0;   // amount in naira OR percentage (0–100)
    public bool HasLateFee => DueDate.HasValue && LateFeeAmount > 0;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<FeeLineItem> LineItems { get; set; } = new List<FeeLineItem>();
    public ICollection<FeeAssignment> Assignments { get; set; } = new List<FeeAssignment>();
}
