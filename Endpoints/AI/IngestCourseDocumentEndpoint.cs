using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services.AI;

namespace LMS.Api.Endpoints.Agent;

public class IngestCourseDocumentRequest
{
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
}

public class IngestCourseDocumentResponse
{
    public int ChunksIngested { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
}

public class IngestCourseDocumentEndpoint : ApiEndpoint<IngestCourseDocumentRequest, IngestCourseDocumentResponse>
{
    private readonly ICourseRagService _ragService;

    public IngestCourseDocumentEndpoint(ICourseRagService ragService)
    {
        _ragService = ragService;
    }

    public override void Configure()
    {
        Post("ai/courses/ingest");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Ingests and indexes course document text for vector RAG search";
            s.Description = "Chunks text, generates vector term embeddings, and saves into the vector database for Tutor Agent citations.";
        });
    }

    public override async Task HandleAsync(IngestCourseDocumentRequest req, CancellationToken ct)
    {
        int count = await _ragService.IngestDocumentAsync(req.CourseId, req.CourseCode, req.DocumentTitle, req.FullText, ct);
        await SendSuccessAsync(new IngestCourseDocumentResponse
        {
            ChunksIngested = count,
            DocumentTitle = req.DocumentTitle
        }, ct, "Document ingested successfully into vector database");
    }
}
