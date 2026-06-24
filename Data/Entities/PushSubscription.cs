using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class PushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = null!;
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [JsonIgnore]
    public AppUser User { get; set; } = null!;
}
