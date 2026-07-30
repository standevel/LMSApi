using LMS.Api.Data.Entities.AI;

namespace LMS.Api.Services.AI;

public interface ICourseRagService
{
    Task<int> IngestDocumentAsync(Guid courseId, string courseCode, string documentTitle, string fullText, CancellationToken ct = default);
    Task<List<CourseDocumentChunk>> SearchRelevantChunksAsync(string query, Guid? courseId = null, string? courseCode = null, int topK = 3, CancellationToken ct = default);
}
