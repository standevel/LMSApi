using System;
using System.Text.Json.Serialization;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

public sealed class ReportCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ReportType ReportType { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? AcademicSessionId { get; set; }
    public string? CacheKey { get; set; } = string.Empty;
    public string? CachedData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public string GetData() => IsExpired ? string.Empty : CachedData ?? string.Empty;
    public bool IsValid() => !IsExpired && !string.IsNullOrEmpty(CachedData);
}
