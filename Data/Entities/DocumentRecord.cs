using System;

namespace LMS.Api.Data.Entities;

public sealed class DocumentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // mime type
    public long FileSize { get; set; }

    public Guid DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    public Guid? OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public Guid? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Specific overrides for this record
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? RejectionReason { get; set; }
    public string AccessMetadata { get; set; } = "{}";
}
