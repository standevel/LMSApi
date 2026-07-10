using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class ProcessCertificateRequestEndpoint : ApiEndpoint<ProcessCertificateRequestRequest, CertificateRequestDto>
{
    private readonly ICertificateService _certificateService;
    private readonly ICurrentUserContext _currentUserContext;

    public ProcessCertificateRequestEndpoint(ICertificateService certificateService, ICurrentUserContext currentUserContext)
    {
        _certificateService = certificateService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("reports/certificate-requests/{requestId:guid}/process");
        Tags("Reporting");
    }

    public override async Task HandleAsync(ProcessCertificateRequestRequest request, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var requestId = Route<Guid>("requestId");
        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
            return;
        }

        var result = await _certificateService.ProcessCertificateRequestAsync(
            requestId, 
            userId.Value, 
            request.BypassGraduationCheck, 
            ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
