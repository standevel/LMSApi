using System;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Persisted official result for a student in a course offering upon publication
/// </summary>
public sealed class StudentCourseResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid CourseOfferingId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicSessionId { get; set; }
    public int Semester { get; set; }
    public int CreditUnits { get; set; }

    public decimal? Ca1Score { get; set; }
    public decimal? Ca2Score { get; set; }
    public decimal? Ca3Score { get; set; }
    public decimal? ExamScore { get; set; }

    public decimal TotalScore { get; set; }
    public string LetterGrade { get; set; } = string.Empty;
    public decimal GradePoints { get; set; }

    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }

    /// <summary>
    /// Snapshot of configuration and weights at publication time for audit & historical fidelity
    /// </summary>
    public string? CalculationSnapshotJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public CourseOffering CourseOffering { get; set; } = null!;
    public AppUser Student { get; set; } = null!;
    public AcademicSession AcademicSession { get; set; } = null!;
    public AppUser? PublishedBy { get; set; }
}
