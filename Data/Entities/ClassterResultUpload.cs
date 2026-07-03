using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class ClassterResultUpload
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UploadId { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public Guid AcademicSessionId { get; set; }
    public Guid CourseId { get; set; }
    public Guid CreatedById { get; set; }
    public ClassterUploadStatus Status { get; set; } = ClassterUploadStatus.Pending;
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SuccessfulRows { get; set; }
    public int FailedRows { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [JsonIgnore]
    public AppUser? CreatedBy { get; set; }

    [JsonIgnore]
    public List<ClassterResultUploadRow> Rows { get; set; } = new();
}

public enum ClassterUploadStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
