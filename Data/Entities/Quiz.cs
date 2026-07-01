using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities
{
    public class Quiz
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        [Column(TypeName = "int")]
        public int? TimeLimitMinutes { get; set; }

        public Guid CourseOfferingId { get; set; }
        public CourseOffering? CourseOffering { get; set; }

        public Guid? AssessmentCategoryId { get; set; }
        public AssessmentCategory? AssessmentCategory { get; set; }

        // Quiz status for scheduling workflow
        public string Status { get; set; } = "Draft"; // Draft, Scheduled, Published, Archived, Closed

        // Scheduling
        public DateTime? OpenDateUtc { get; set; }
        public DateTime? CloseDateUtc { get; set; }

        // Pass threshold
        public decimal? PassThreshold { get; set; }
        
        public bool ReminderSent { get; set; } = false;

        public string TargetProgramIdsJson { get; set; } = "[]";

        // Relationships
        public QuizSetting? Setting { get; set; }
        public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
        public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
        public ICollection<QuizFeedback> Feedbacks { get; set; } = new List<QuizFeedback>();
        public ICollection<QuizTimeExtension> TimeExtensions { get; set; } = new List<QuizTimeExtension>();
        public ICollection<QuestionBankItem> BankItems { get; set; } = new List<QuestionBankItem>();

        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
