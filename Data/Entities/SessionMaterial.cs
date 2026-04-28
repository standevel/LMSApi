using System;

namespace LMS.Api.Data.Entities;

public sealed class SessionMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LectureSessionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedBy { get; set; }

    // Navigation properties
    public LectureSession LectureSession { get; set; } = null!;
    public AppUser UploadedByUser { get; set; } = null!;
}
