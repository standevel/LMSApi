namespace LMS.Api.Data.Entities.AI;

public sealed class CourseDocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string ChunkText { get; set; } = string.Empty;
    public int? PageOrSlideNumber { get; set; }
    public string EmbeddingVectorJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
