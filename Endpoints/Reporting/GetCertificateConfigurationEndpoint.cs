using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetCertificateConfigurationEndpoint : ApiEndpointWithoutRequest<SystemCertificateConfigurationDto>
{
    private readonly ICertificateService _certificateService;

    public GetCertificateConfigurationEndpoint(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    public override void Configure()
    {
        Get("reports/certificate-configuration");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var result = await _certificateService.GetConfigurationAsync(ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
