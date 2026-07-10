using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class CertificateRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public CertificateType CertificateType { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.Pending;
    public string? DeliveryMethod { get; set; } = "Email"; // Email, Pickup, Mail
    public string? DeliveryEmail { get; set; }
    public string? Remarks { get; set; }
    public decimal? FeeAmount { get; set; }
    public bool FeePaid { get; set; } = false;
    public string? DocumentUrl { get; set; }
    public string CredentialId { get; set; } = string.Empty; // Unique verify code, e.g. WWU-CERT-2026-XXXX
    public DateTime? CompletedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public Guid? ProcessedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public Student? Student { get; set; }
    [JsonIgnore]
    public AppUser? Creator { get; set; }
    [JsonIgnore]
    public AppUser? Processor { get; set; }
}
