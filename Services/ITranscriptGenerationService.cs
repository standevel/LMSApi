using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ITranscriptGenerationService
{
    Task<ErrorOr<TranscriptDto>> GenerateTranscriptAsync(Guid studentId, bool isOfficial = true, CancellationToken ct = default);
    Task<ErrorOr<TranscriptRequestDto>> CreateTranscriptRequestAsync(Guid studentId, CreateTranscriptRequestDto request, Guid requestedBy, CancellationToken ct = default);
    Task<ErrorOr<TranscriptRequestDto>> GetTranscriptRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<ErrorOr<List<TranscriptRequestDto>>> GetStudentTranscriptRequestsAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<List<TranscriptRequestDto>>> GetAllTranscriptRequestsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ErrorOr<TranscriptRequestDto>> ProcessTranscriptRequestAsync(Guid requestId, Guid processedBy, CancellationToken ct = default);
}
