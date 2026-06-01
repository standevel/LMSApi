using System;

namespace LMS.Api.Data.Entities;

public sealed class ApiRateLimit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty; // Could be user ID, IP, or API key
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public DateTime WindowStartUtc { get; set; }
    public int RequestCount { get; set; } = 0;
    public int Limit { get; set; } = 100;
    public int Remaining { get; set; } = 100;
    public DateTime? ResetTimeUtc { get; set; }
}