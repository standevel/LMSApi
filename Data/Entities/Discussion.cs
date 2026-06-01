using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class DiscussionThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public Guid? AuthorId { get; set; }
    public Guid? CourseOfferingId { get; set; } // Optional: tie to a specific course offering
    public bool IsPinned { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [JsonIgnore]
    public AppUser? Author { get; set; }
    [JsonIgnore]
    public CourseOffering? CourseOffering { get; set; }
    public ICollection<DiscussionPost> Posts { get; set; } = [];
}

public sealed class DiscussionPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiscussionThreadId { get; set; }
    public Guid? AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [JsonIgnore]
    public DiscussionThread DiscussionThread { get; set; } = null!;
    [JsonIgnore]
    public AppUser? Author { get; set; }
    public ICollection<DiscussionPostAttachment> Attachments { get; set; } = [];
}

// For attachments in discussion posts
public sealed class DiscussionPostAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DiscussionPostId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    // Navigation
    public DiscussionPost DiscussionPost { get; set; } = null!;
}