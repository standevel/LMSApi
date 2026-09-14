using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Endpoints.Reporting;

public sealed class CreateTranscriptRequestEndpoint : ApiEndpoint<CreateTranscriptRequestDto, TranscriptRequestDto>
{
    private readonly ITranscriptGenerationService _transcriptService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<CreateTranscriptRequestEndpoint> _logger;

    public CreateTranscriptRequestEndpoint(
        ITranscriptGenerationService transcriptService,
        ICurrentUserContext currentUserContext,
        ILogger<CreateTranscriptRequestEndpoint> logger)
    {
        _transcriptService = transcriptService;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("reports/transcript-requests");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CreateTranscriptRequestDto request, CancellationToken ct)
    {
        try
        {
            if (HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
                return;
            }

            var studentId = request.StudentId;
            if (!studentId.HasValue)
            {
                studentId = await _currentUserContext.GetUserIdAsync(ct);
            }

            if (!studentId.HasValue)
            {
                await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
                return;
            }

            var userId = await _currentUserContext.GetUserIdAsync(ct);
            if (!userId.HasValue)
            {
                await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
                return;
            }

            var result = await _transcriptService.CreateTranscriptRequestAsync(studentId.Value, request, userId.Value, ct);

            if (result.IsError)
            {
                var error = result.FirstError;
                var statusCode = error.Type switch
                {
                    ErrorType.NotFound => 404,
                    _ => 400
                };
                await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
                return;
            }

            await SendCreatedAsync(result.Value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while creating transcript request for student {StudentId}", request.StudentId);
            await SendFailureAsync(500, "An unexpected error occurred while processing the transcript request: " + ex.Message, "INTERNAL_SERVER_ERROR", ex.Message, ct);
        }
    }
}
