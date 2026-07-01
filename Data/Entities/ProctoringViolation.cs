using System;
using System.ComponentModel.DataAnnotations;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class ProctoringViolation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SessionId { get; set; }

    public ExamProctoringSession? Session { get; set; }

    [Required]
    public string ViolationType { get; set; } = string.Empty;

    public string? Details { get; set; }

    public string? ScreenshotUrl { get; set; }

    [Required]
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public int Severity { get; set; } = 1; // 1=low, 2=medium, 3=high
}
