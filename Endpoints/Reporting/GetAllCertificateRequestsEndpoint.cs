using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetAllCertificateRequestsEndpoint : ApiEndpointWithoutRequest<List<CertificateRequestDto>>
{
    private readonly ICertificateService _certificateService;

    public GetAllCertificateRequestsEndpoint(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    public override void Configure()
    {
        Get("reports/certificate-requests");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var pageNumber = QueryParam<int>("pageNumber") ?? 1;
        var pageSize = QueryParam<int>("pageSize") ?? 20;
        var result = await _certificateService.GetAllCertificateRequestsAsync(pageNumber, pageSize, ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
