using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetSystemGradingConfigurationEndpoint : ApiEndpointWithoutRequest<SystemGradingConfigurationDto>
{
    private readonly IGradebookService _gradebookService;

    public GetSystemGradingConfigurationEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("/api/gradebook/system-configuration");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _gradebookService.GetSystemConfigurationAsync(ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            await SendFailureAsync(400, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
