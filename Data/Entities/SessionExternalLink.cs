using System;

namespace LMS.Api.Data.Entities;

public sealed class SessionExternalLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LectureSessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public LectureSession LectureSession { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
}
