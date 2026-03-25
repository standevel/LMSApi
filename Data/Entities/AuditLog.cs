using System;

namespace LMS.Api.Data.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Toggle
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Changes { get; set; } // JSON or summary of changes
    public Guid? UserId { get; set; } // Who performed the action
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
