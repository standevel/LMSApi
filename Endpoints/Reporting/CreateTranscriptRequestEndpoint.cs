using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class CreateTranscriptRequestEndpoint : ApiEndpoint<CreateTranscriptRequestDto, TranscriptRequestDto>
{
    private readonly ITranscriptGenerationService _transcriptService;
    private readonly ICurrentUserContext _currentUserContext;

    public CreateTranscriptRequestEndpoint(ITranscriptGenerationService transcriptService, ICurrentUserContext currentUserContext)
    {
        _transcriptService = transcriptService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("api/reports/transcript-requests");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CreateTranscriptRequestDto request, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var studentId = Route<Guid?>("studentId");
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
}
