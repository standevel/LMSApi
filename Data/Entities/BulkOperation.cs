using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class BulkOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OperationType { get; set; } = string.Empty; // e.g., "UserImport", "EnrollmentImport"
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ResultData { get; set; } // JSON with results
    public BulkOperationStatus Status { get; set; } = BulkOperationStatus.Pending;
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int FailedRecords { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedById { get; set; }
    
    [JsonIgnore]
    public AppUser? CreatedBy { get; set; }
}

public enum BulkOperationStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}