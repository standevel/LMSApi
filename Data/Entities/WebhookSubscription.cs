using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string EventTypes { get; set; } = string.Empty; // JSON array of event types
    public bool IsActive { get; set; } = true;
    public int RetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedById { get; set; }
    
    [JsonIgnore]
    public AppUser? CreatedBy { get; set; }
    public ICollection<WebhookDeliveryLog> DeliveryLogs { get; set; } = new List<WebhookDeliveryLog>();
}