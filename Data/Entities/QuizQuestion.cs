using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    [Required]
    public string QuestionText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = string.Empty;

    // Point value for this question (enables weighted questions)
    public int Points { get; set; } = 1;

    // Difficulty and category for organization
    public string? Difficulty { get; set; } // Easy, Medium, Hard
    public string? Category { get; set; } // Topic/category
    public string? Tags { get; set; } // Comma-separated tags

    // Explanation shown after submission
    public string? Explanation { get; set; }

    // For random pool sources
    public Guid? SourceBankItemId { get; set; }
    public QuestionBankItem? SourceBankItem { get; set; }

    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}

