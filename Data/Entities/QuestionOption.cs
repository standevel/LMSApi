using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Api.Data.Entities;

public class QuestionOption
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string OptionText { get; set; } = string.Empty; // The text of the option/answer choice

    // Relationship to QuizQuestion (Which question does this option belong to?)
    public Guid QuizQuestionId { get; set; }
    public QuizQuestion QuizQuestion { get; set; } = null!;

    [Required]
    public int DisplayOrder { get; set; } // Order of display for multiple choice options
    
    // For grading purposes, this might store the correct answer index/ID (if not already done by a separate Answer entity)
    public bool IsCorrectAnswer { get; set; } = false; 
}
