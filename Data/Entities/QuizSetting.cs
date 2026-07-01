using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Quiz-level settings: shuffle, attempts, dates, pass threshold, random pool, etc.
/// </summary>
public class QuizSetting
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    // Shuffle settings
    public bool ShuffleQuestions { get; set; } = false;
    public bool ShuffleOptions { get; set; } = false;

    // Attempt control
    public int MaxAttempts { get; set; } = 1;
    public bool AllowPartialCredit { get; set; } = true;
    public bool ScoreBestAttempt { get; set; } = false; // If true, use best attempt score; otherwise use last

    // Date scheduling
    public DateTime? OpenDateUtc { get; set; }
    public DateTime? CloseDateUtc { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Scheduled, Published, Archived, Closed

    // Pass threshold
    public decimal? PassThreshold { get; set; } // Minimum percentage to pass (e.g., 50 = 50%)

    // Random pool settings
    public bool UseRandomPool { get; set; } = false;
    public int? PoolSize { get; set; } // Number of questions to randomly select from pool
    public Guid? PoolQuestionBankId { get; set; } // Question bank to pull random questions from
    public QuestionBank? PoolQuestionBank { get; set; }

    // Feedback control
    public string FeedbackVisibility { get; set; } = "Immediate"; // Immediate, AfterClose, Manual, Never

    // Exam integrity
    public bool RequireFullscreen { get; set; } = false;
    public bool AllowTabSwitchDetection { get; set; } = true;
    public int MaxTabSwitches { get; set; } = 3;

    // Access control
    public string? AccessCode { get; set; } // PIN/code required to start quiz
    public bool RestrictToAllowedIps { get; set; } = false;
    public string? AllowedIpRangesJson { get; set; }
    public string? AllowedCbtHallIdsJson { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
