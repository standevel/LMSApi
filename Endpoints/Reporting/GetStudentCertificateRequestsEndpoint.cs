using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class GetStudentCertificateRequestsEndpoint : ApiEndpointWithoutRequest<List<CertificateRequestDto>>
{
    private readonly ICertificateService _certificateService;

    public GetStudentCertificateRequestsEndpoint(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    public override void Configure()
    {
        Get("reports/certificate-requests/student/{studentId:guid}");
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
        var result = await _certificateService.GetStudentCertificateRequestsAsync(studentId, ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
