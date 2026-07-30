using System.Text.Json;
using LMS.Api.Data;
using LMS.Api.Data.Entities.AI;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services.AI;

public class CourseRagService : ICourseRagService
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<CourseRagService> _logger;

    public CourseRagService(LmsDbContext dbContext, ILogger<CourseRagService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private async Task EnsureTableCreatedAsync(CancellationToken ct)
    {
        try
        {
            const string sql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CourseDocumentChunks')
            BEGIN
                CREATE TABLE [CourseDocumentChunks] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [CourseId] uniqueidentifier NOT NULL,
                    [CourseCode] nvarchar(max) NOT NULL,
                    [DocumentTitle] nvarchar(max) NOT NULL,
                    [ChunkText] nvarchar(max) NOT NULL,
                    [PageOrSlideNumber] int NULL,
                    [EmbeddingVectorJson] nvarchar(max) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL
                );
            END";
            await _dbContext.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not execute EnsureTableCreatedAsync for CourseDocumentChunks table.");
        }
    }

    public async Task<int> IngestDocumentAsync(Guid courseId, string courseCode, string documentTitle, string fullText, CancellationToken ct = default)
    {
        await EnsureTableCreatedAsync(ct);

        _logger.LogInformation("Ingesting course document '{Title}' for Course {CourseCode}", documentTitle, courseCode);

        // Text chunking by paragraph/sentence markers
        var rawChunks = fullText.Split(new[] { "\n\n", "\r\n\r\n", ". " }, StringSplitOptions.RemoveEmptyEntries)
            .Where(c => c.Trim().Length > 20)
            .ToList();

        var chunksToSave = new List<CourseDocumentChunk>();

        for (int i = 0; i < rawChunks.Count; i++)
        {
            var text = rawChunks[i].Trim();
            var vector = GenerateLocalTermVector(text);

            chunksToSave.Add(new CourseDocumentChunk
            {
                CourseId = courseId,
                CourseCode = courseCode,
                DocumentTitle = documentTitle,
                ChunkText = text,
                PageOrSlideNumber = (i / 3) + 1,
                EmbeddingVectorJson = JsonSerializer.Serialize(vector),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.CourseDocumentChunks.AddRangeAsync(chunksToSave, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Successfully ingested {Count} vector chunks for '{Title}'", chunksToSave.Count, documentTitle);
        return chunksToSave.Count;
    }

    public async Task<List<CourseDocumentChunk>> SearchRelevantChunksAsync(string query, Guid? courseId = null, string? courseCode = null, int topK = 3, CancellationToken ct = default)
    {
        try
        {
            await EnsureTableCreatedAsync(ct);

            var queryVector = GenerateLocalTermVector(query);

            var queryable = _dbContext.CourseDocumentChunks.AsQueryable();
            if (courseId.HasValue && courseId.Value != Guid.Empty)
            {
                queryable = queryable.Where(x => x.CourseId == courseId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(courseCode))
            {
                queryable = queryable.Where(x => x.CourseCode == courseCode);
            }

            var allChunks = await queryable.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);

            if (allChunks.Count == 0)
            {
                return new List<CourseDocumentChunk>();
            }

            // Rank by Cosine Similarity against query vector
            var ranked = allChunks
                .Select(chunk =>
                {
                    var chunkVector = JsonSerializer.Deserialize<Dictionary<string, double>>(chunk.EmbeddingVectorJson) ?? new();
                    double similarity = CalculateCosineSimilarity(queryVector, chunkVector);
                    return new { Chunk = chunk, Score = similarity };
                })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Chunk)
                .ToList();

            return ranked;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching CourseDocumentChunks table. Returning empty fallback list.");
            return new List<CourseDocumentChunk>();
        }
    }

    private static Dictionary<string, double> GenerateLocalTermVector(string text)
    {
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '-', '(', ')', '[', ']', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToList();

        var vector = new Dictionary<string, double>();
        foreach (var word in words)
        {
            if (vector.ContainsKey(word)) vector[word] += 1.0;
            else vector[word] = 1.0;
        }

        // Normalize
        double mag = Math.Sqrt(vector.Values.Sum(v => v * v));
        if (mag > 0)
        {
            foreach (var key in vector.Keys.ToList())
            {
                vector[key] /= mag;
            }
        }

        return vector;
    }

    private static double CalculateCosineSimilarity(Dictionary<string, double> vecA, Dictionary<string, double> vecB)
    {
        double dotProduct = 0;
        foreach (var kvp in vecA)
        {
            if (vecB.TryGetValue(kvp.Key, out double valB))
            {
                dotProduct += kvp.Value * valB;
            }
        }
        return dotProduct;
    }
}
