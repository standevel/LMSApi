using System;

namespace LMS.Api.Data.Entities;

public sealed class SystemTranscriptConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool ChargeForTranscripts { get; set; } = true;
    public decimal OfficialTranscriptFee { get; set; } = 5000m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
