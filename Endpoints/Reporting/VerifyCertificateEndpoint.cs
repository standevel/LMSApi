using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class VerifyCertificateEndpoint : ApiEndpointWithoutRequest<CertificateVerificationDto>
{
    private readonly ICertificateService _certificateService;

    public VerifyCertificateEndpoint(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    public override void Configure()
    {
        Get("reports/certificates/verify/{credentialId}");
        AllowAnonymous();
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var credentialId = Route<string>("credentialId");
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            await SendFailureAsync(400, "Credential ID is required", "VALIDATION_ERROR", "Credential ID must be provided", ct);
            return;
        }

        var result = await _certificateService.VerifyCertificateAsync(credentialId, ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
