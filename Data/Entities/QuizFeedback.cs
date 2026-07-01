using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Per-question or per-attempt feedback for students after quiz submission.
/// </summary>
public class QuizFeedback
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;

    public Guid? QuestionId { get; set; } // Null = quiz-level feedback; set = question-level
    public QuizQuestion? Question { get; set; }

    [Required]
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Required]
    public string FeedbackText { get; set; } = string.Empty;

    public string FeedbackType { get; set; } = "General"; // General, QuestionSpecific, GradingNote, Encouragement

    // Grading notes for the lecturer
    public string? GradingNotes { get; set; }
    public decimal? ManualOverrideScore { get; set; } // Manual score adjustment

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
