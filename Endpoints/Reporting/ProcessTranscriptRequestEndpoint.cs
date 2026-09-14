using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Endpoints.Reporting;

public sealed class ProcessTranscriptRequestEndpoint : ApiEndpointWithoutRequest<TranscriptRequestDto>
{
    private readonly ITranscriptGenerationService _transcriptService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<ProcessTranscriptRequestEndpoint> _logger;

    public ProcessTranscriptRequestEndpoint(
        ITranscriptGenerationService transcriptService,
        ICurrentUserContext currentUserContext,
        ILogger<ProcessTranscriptRequestEndpoint> logger)
    {
        _transcriptService = transcriptService;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("reports/transcript-requests/{requestId:guid}/process");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            if (HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
                return;
            }

            var requestId = Route<Guid>("requestId");
            var userId = await _currentUserContext.GetUserIdAsync(ct);
            if (!userId.HasValue)
            {
                await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
                return;
            }

            var result = await _transcriptService.ProcessTranscriptRequestAsync(requestId, userId.Value, ct);

            if (result.IsError)
            {
                var error = result.FirstError;
                var statusCode = error.Type switch
                {
                    ErrorType.NotFound => 404,
                    ErrorType.Forbidden => 403,
                    ErrorType.Conflict => 409,
                    _ => 400
                };
                await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
                return;
            }

            await SendSuccessAsync(result.Value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process transcript request");
            await SendFailureAsync(500, "Failed to process transcript request: " + ex.Message, "INTERNAL_SERVER_ERROR", ex.Message, ct);
        }
    }
}
