using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Reporting;

public sealed class CreateCertificateRequestEndpoint : ApiEndpoint<CreateCertificateRequestDto, CertificateRequestDto>
{
    private readonly ICertificateService _certificateService;
    private readonly ICurrentUserContext _currentUserContext;

    public CreateCertificateRequestEndpoint(ICertificateService certificateService, ICurrentUserContext currentUserContext)
    {
        _certificateService = certificateService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("reports/certificate-requests");
        Tags("Reporting");
    }

    public override async Task HandleAsync(CreateCertificateRequestDto request, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var studentId = request.StudentId;
        if (!studentId.HasValue)
        {
            studentId = await _currentUserContext.GetUserIdAsync(ct);
        }

        if (!studentId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);
            return;
        }

        var result = await _certificateService.CreateCertificateRequestAsync(studentId.Value, request, userId.Value, ct);

        if (result.IsError)
        {
            await HandleErrorAsync(result.Errors, ct);
            return;
        }

        await SendCreatedAsync(result.Value, ct);
    }
}
