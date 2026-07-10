using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class UpdateCertificateConfigurationEndpoint : ApiEndpoint<UpdateSystemCertificateConfigurationRequest, SystemCertificateConfigurationDto>
{
    private readonly ICertificateService _certificateService;
    private readonly ICurrentUserContext _currentUserContext;

    public UpdateCertificateConfigurationEndpoint(ICertificateService certificateService, ICurrentUserContext currentUserContext)
    {
        _certificateService = certificateService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Put("reports/certificate-configuration");
        Tags("Reporting");
    }

    public override async Task HandleAsync(UpdateSystemCertificateConfigurationRequest request, CancellationToken ct)
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

        var result = await _certificateService.UpdateConfigurationAsync(request, userId.Value, ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
