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
    public AppUser? Student { get; set; }

    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    [Required]
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    
    [Required]
    public string Status { get; set; } = "Active";

    public int ViolationCount { get; set; } = 0;

    // Camera & selfie
    public string? SelfieCaptureUrl { get; set; }
    public bool CameraPermissionGranted { get; set; } = false;

    // Tab switch tracking
    public int TabSwitchCount { get; set; } = 0;

    // Fullscreen tracking
    public bool IsFullscreen { get; set; } = true;
    public int FullscreenLossCount { get; set; } = 0;

    // Network & device info
    public string? BrowserInfo { get; set; }
    public string? ScreenResolution { get; set; }
    public string? UserAgent { get; set; }
    public string? IPAddress { get; set; }

    // Integrity score (0-100)
    public decimal IntegrityScore { get; set; } = 100m;

    // Relationships
    public ICollection<ProctoringViolation> Violations { get; set; } = new List<ProctoringViolation>();
}
