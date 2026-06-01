using System;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class ExamProctoringSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    [Required]
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    
    [Required]
    public string Status { get; set; } = "Active";

    public int ViolationCount { get; set; } = 0;
}
