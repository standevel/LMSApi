using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetStudentTranscriptRequestsEndpoint : ApiEndpointWithoutRequest<List<TranscriptRequestDto>>
{
    private readonly ITranscriptGenerationService _transcriptService;

    public GetStudentTranscriptRequestsEndpoint(ITranscriptGenerationService transcriptService)
    {
        _transcriptService = transcriptService;
    }

public override void Configure()
{
    Get("reports/transcript-requests/student/{studentId:guid}");
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
        var result = await _transcriptService.GetStudentTranscriptRequestsAsync(studentId, ct);

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
