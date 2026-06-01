using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [JsonIgnore]
    public AppUser? Sender { get; set; }
    [JsonIgnore]
    public AppUser? Recipient { get; set; }
    public ICollection<MessageAttachment> Attachments { get; set; } = [];
}

// For attachments in messages
public sealed class MessageAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    // Navigation
    public Message Message { get; set; } = null!;
}