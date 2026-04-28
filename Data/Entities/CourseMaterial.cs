using System;

namespace LMS.Api.Data.Entities;

public sealed class CourseMaterial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public Guid UploadedById { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public CourseOffering CourseOffering { get; set; } = null!;
    public AppUser UploadedBy { get; set; } = null!;
}
