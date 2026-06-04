using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetTranscriptRequestEndpoint : ApiEndpointWithoutRequest<TranscriptRequestDto>
{
    private readonly ITranscriptGenerationService _transcriptService;

    public GetTranscriptRequestEndpoint(ITranscriptGenerationService transcriptService)
    {
        _transcriptService = transcriptService;
    }

public override void Configure()
{
    Get("reports/transcript-requests/{requestId:guid}");
    Tags("Reporting");
}

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var requestId = Route<Guid>("requestId");
        var result = await _transcriptService.GetTranscriptRequestAsync(requestId, ct);

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

        await SendSuccessAsync(result.Value, ct);
    }
}
