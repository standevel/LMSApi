using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public Guid? AuthorId { get; set; }
    public bool IsGlobal { get; set; } = false; // If true, visible to all users; otherwise, maybe targeted?
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; } // Optional expiration date

    // Navigation properties
    [JsonIgnore]
    public AppUser? Author { get; set; }
    public ICollection<AnnouncementAttachment> Attachments { get; set; } = [];
}

// For attachments in announcements
public sealed class AnnouncementAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnnouncementId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    // Navigation
    public Announcement Announcement { get; set; } = null!;
}