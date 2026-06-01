using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GenerateTranscriptEndpoint : ApiEndpointWithoutRequest<TranscriptDto>
{
    private readonly ITranscriptGenerationService _transcriptService;
    private readonly ICurrentUserContext _currentUserContext;

    public GenerateTranscriptEndpoint(ITranscriptGenerationService transcriptService, ICurrentUserContext currentUserContext)
    {
        _transcriptService = transcriptService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Get("api/reports/transcript/{studentId:guid}");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var studentId = Route<Guid>("studentId");
        var isOfficial = QueryParam<bool>("official") ?? true;
        var result = await _transcriptService.GenerateTranscriptAsync(studentId, isOfficial, ct);

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
