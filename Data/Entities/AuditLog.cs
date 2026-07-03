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
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public string? QueryString { get; set; }
    public int? StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestContentType { get; set; }
    public string? RequestBodyJson { get; set; }

    public AppUser? User { get; set; }
}
