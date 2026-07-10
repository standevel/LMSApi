using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class UpdateTranscriptConfigurationEndpoint : ApiEndpoint<UpdateSystemTranscriptConfigurationRequest, SystemTranscriptConfigurationDto>
{
    private readonly ITranscriptGenerationService _transcriptService;
    private readonly ICurrentUserContext _currentUserContext;

    public UpdateTranscriptConfigurationEndpoint(ITranscriptGenerationService transcriptService, ICurrentUserContext currentUserContext)
    {
        _transcriptService = transcriptService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Put("reports/transcript-configuration");
        Tags("Reporting");
    }

    public override async Task HandleAsync(UpdateSystemTranscriptConfigurationRequest request, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
            return;
        }

        var result = await _transcriptService.UpdateConfigurationAsync(request, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
