using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientId { get; set; }
    public Guid? SenderId { get; set; } // Optional: who triggered the notification (could be system)
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NotificationType { get; set; } // e.g., "AssignmentDue", "NewAnnouncement", etc.
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public string? RelatedUrl { get; set; } // Optional link to related resource
    public bool IsActive { get; set; } = true;

    // Navigation properties
    [JsonIgnore]
    public AppUser? Recipient { get; set; }
    [JsonIgnore]
    public AppUser? Sender { get; set; }
}