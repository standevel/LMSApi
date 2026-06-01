using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class TranscriptRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public TranscriptStatus Status { get; set; } = TranscriptStatus.Pending;
    public bool IsOfficial { get; set; } = true;
    public string? DeliveryEmail { get; set; }
    public string? DeliveryMethod { get; set; } = "Email"; // Email, Pickup, Mail
    public string? Remarks { get; set; }
    public decimal? FeeAmount { get; set; }
    public bool FeePaid { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public string? DocumentUrl { get; set; }
    public Guid? CreatedById { get; set; }
    public Guid? ProcessedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public AppUser? Student { get; set; }
    [JsonIgnore]
    public AppUser? Creator { get; set; }
    [JsonIgnore]
    public AppUser? Processor { get; set; }
}
