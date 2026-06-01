using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class WebhookDeliveryLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WebhookSubscriptionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty; // JSON payload
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuccess { get; set; }
    public int AttemptNumber { get; set; }
    
    [JsonIgnore]
    public WebhookSubscription? WebhookSubscription { get; set; }
}