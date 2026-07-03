using System;
using System.Text.Json.Serialization;

namespace LMS.Api.Data.Entities;

public sealed class ClassterResultUploadRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UploadId { get; set; }
    public int RowNumber { get; set; }
    public string? ExternalStudentId { get; set; }
    public string? StudentName { get; set; }
    public string? AssessmentType { get; set; }
    public decimal? MarksObtained { get; set; }
    public int? AttemptNumber { get; set; }
    public string? Fingerprint { get; set; }
    public string MappingStatus { get; set; } = "Pending";
    public string? MappingReason { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? CourseOfferingId { get; set; }
    public Guid? AssessmentId { get; set; }
    public string? RawPayload { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ClassterResultUpload? Upload { get; set; }
}
