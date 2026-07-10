using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetTranscriptConfigurationEndpoint : ApiEndpointWithoutRequest<SystemTranscriptConfigurationDto>
{
    private readonly ITranscriptGenerationService _transcriptService;

    public GetTranscriptConfigurationEndpoint(ITranscriptGenerationService transcriptService)
    {
        _transcriptService = transcriptService;
    }

    public override void Configure()
    {
        Get("reports/transcript-configuration");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var result = await _transcriptService.GetConfigurationAsync(ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
