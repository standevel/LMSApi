using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Stores individual questions in a QuestionBank for reuse across quizzes.
/// </summary>
public class QuestionBankItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid QuestionBankId { get; set; }
    public QuestionBank QuestionBank { get; set; } = null!;

    // Question content
    [Required]
    public string QuestionText { get; set; } = string.Empty;
    public string? QuestionType { get; set; } // MCQ, TrueFalse, Essay, ShortAnswer, FileUpload
    public int? Points { get; set; } // Point value for this question

    // Options for MCQ/TrueFalse
    public ICollection<QuestionBankOption> Options { get; set; } = new List<QuestionBankOption>();

    // Correct answer tracking
    public string? CorrectAnswer { get; set; } // For auto-grading (MCQ, TrueFalse)
    public string? CorrectOptionId { get; set; } // Reference to the correct option

    // Metadata for organization
    public string? Category { get; set; } // Topic/category tag
    public string? Difficulty { get; set; } // Easy, Medium, Hard
    public string? Tags { get; set; } // Comma-separated tags for search
    public string? Explanation { get; set; } // Explanation shown after submission
    public string? Feedback { get; set; } // Per-question feedback for students

    // Usage tracking
    public int TimesUsed { get; set; } = 0;
    public decimal AverageScore { get; set; } = 0; // Average score when this question was used

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Converts this bank item to a QuizQuestion for use in a quiz.
    /// </summary>
    public QuizQuestion ToQuizQuestion(int orderIndex)
    {
        var quizQuestion = new QuizQuestion
        {
            Id = Guid.NewGuid(),
            QuizId = Guid.Empty, // Will be set when added to a quiz
            QuestionText = this.QuestionText,
            OrderIndex = orderIndex,
            QuestionType = this.QuestionType ?? "MCQ",
            Points = this.Points ?? 1,
            Difficulty = this.Difficulty,
            Category = this.Category,
            Tags = this.Tags,
            Explanation = this.Explanation,
            Options = new List<QuestionOption>()
        };

        // Convert bank options to quiz options
        foreach (var bankOption in this.Options.OrderBy(o => o.DisplayOrder))
        {
            quizQuestion.Options.Add(new QuestionOption
            {
                Id = Guid.NewGuid(),
                OptionText = bankOption.OptionText,
                DisplayOrder = bankOption.DisplayOrder,
                IsCorrectAnswer = bankOption.IsCorrectAnswer
            });
        }

        return quizQuestion;
    }
}

/// <summary>
/// Options/stem items stored within a QuestionBankItem (for MCQ, TrueFalse, etc.)
/// </summary>
public class QuestionBankOption
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid QuestionBankItemId { get; set; }
    public QuestionBankItem QuestionBankItem { get; set; } = null!;

    [Required]
    public string OptionText { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsCorrectAnswer { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
