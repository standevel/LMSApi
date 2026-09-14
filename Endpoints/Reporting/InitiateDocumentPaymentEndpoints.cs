using System.Security.Claims;
using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Endpoints.Reporting;

public class InitiateTranscriptPaymentRequest
{
    public Guid RequestId { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
    public string Gateway { get; set; } = "Paystack";
}

public sealed class InitiateTranscriptPaymentEndpoint : ApiEndpoint<InitiateTranscriptPaymentRequest, GatewayInitResponse>
{
    private readonly LmsDbContext _db;
    private readonly PaystackService _paystackService;
    private readonly HydrogenService _hydrogenService;
    private readonly ILogger<InitiateTranscriptPaymentEndpoint> _logger;

    public InitiateTranscriptPaymentEndpoint(
        LmsDbContext db,
        PaystackService paystackService,
        HydrogenService hydrogenService,
        ILogger<InitiateTranscriptPaymentEndpoint> logger)
    {
        _db = db;
        _paystackService = paystackService;
        _hydrogenService = hydrogenService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("reports/transcript-requests/{RequestId}/pay");
        Tags("Reporting");
    }

    public override async Task HandleAsync(InitiateTranscriptPaymentRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var reqId = req.RequestId != Guid.Empty ? req.RequestId : Route<Guid>("RequestId");
        if (reqId == Guid.Empty)
        {
            reqId = Route<Guid>("requestId");
        }

        var request = await _db.TranscriptRequests.Include(tr => tr.Student).FirstOrDefaultAsync(tr => tr.Id == reqId, ct);
        if (request == null)
        {
            await SendFailureAsync(404, "Not found", "NOT_FOUND", "Transcript request not found.", ct);
            return;
        }

        if (request.FeePaid)
        {
            await SendFailureAsync(400, "Fee already paid.", "ALREADY_PAID", "This request is already paid.", ct);
            return;
        }

        var amount = request.FeeAmount ?? 0m;
        if (amount <= 0)
        {
            await SendFailureAsync(400, "No fee required.", "NO_FEE", "No fee is required for this request.", ct);
            return;
        }

        // Look up student from Students table to get real student data
        var student = request.StudentId != Guid.Empty 
            ? await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StudentId, ct)
            : null;

        var email = !string.IsNullOrWhiteSpace(request.DeliveryEmail) ? request.DeliveryEmail :
                    !string.IsNullOrWhiteSpace(student?.OfficialEmail) ? student.OfficialEmail :
                    !string.IsNullOrWhiteSpace(student?.PersonalEmail) ? student.PersonalEmail :
                    !string.IsNullOrWhiteSpace(request.Student?.Email) ? request.Student.Email :
                    HttpContext.User?.FindFirst(ClaimTypes.Email)?.Value ??
                    HttpContext.User?.FindFirst("preferred_username")?.Value ??
                    "student@wigweuniversity.edu.ng";

        var callbackUrl = !string.IsNullOrWhiteSpace(req.CallbackUrl)
            ? req.CallbackUrl
            : $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/self-service/transcript";

        var gateway = (req.Gateway ?? "Paystack").Trim();
        var gatewayRef = $"LMS-TR-{request.StudentId.ToString("N")[..8]}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();

        try
        {
            if (gateway.Equals("Hydrogen", StringComparison.OrdinalIgnoreCase))
            {
                var customerName = student != null
                    ? $"{student.FirstName} {student.LastName}".Trim()
                    : (request.Student?.DisplayName ?? "Student");

                var (url, txRef) = await _hydrogenService.InitiatePaymentAsync(
                    email, customerName, amount, "Official Transcript Request Fee", callbackUrl,
                    new { transcriptRequestId = request.Id, system = "LMS" });

                await SendSuccessAsync(new GatewayInitResponse(url, txRef), ct);
            }
            else
            {
                var tuple = await _paystackService.InitializeTransactionAsync(
                    email, amount, gatewayRef, callbackUrl,
                    new { transcriptRequestId = request.Id, system = "LMS" });

                await SendSuccessAsync(new GatewayInitResponse(tuple.AuthorizationUrl, tuple.Reference), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize payment gateway ({Gateway}) for transcript request {RequestId}. Email: {Email}", gateway, reqId, email);
            await SendFailureAsync(400, ex.Message, "GATEWAY_ERROR", ex.Message, ct);
        }
    }
}

public class InitiateCertificatePaymentRequest
{
    public Guid RequestId { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
    public string Gateway { get; set; } = "Paystack";
}

public sealed class InitiateCertificatePaymentEndpoint : ApiEndpoint<InitiateCertificatePaymentRequest, GatewayInitResponse>
{
    private readonly LmsDbContext _db;
    private readonly PaystackService _paystackService;
    private readonly HydrogenService _hydrogenService;
    private readonly ILogger<InitiateCertificatePaymentEndpoint> _logger;

    public InitiateCertificatePaymentEndpoint(
        LmsDbContext db,
        PaystackService paystackService,
        HydrogenService hydrogenService,
        ILogger<InitiateCertificatePaymentEndpoint> logger)
    {
        _db = db;
        _paystackService = paystackService;
        _hydrogenService = hydrogenService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("reports/certificate-requests/{RequestId}/pay");
        Tags("Reporting");
    }

    public override async Task HandleAsync(InitiateCertificatePaymentRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var reqId = req.RequestId != Guid.Empty ? req.RequestId : Route<Guid>("RequestId");
        if (reqId == Guid.Empty)
        {
            reqId = Route<Guid>("requestId");
        }

        var request = await _db.CertificateRequests.Include(cr => cr.Student).FirstOrDefaultAsync(cr => cr.Id == reqId, ct);
        if (request == null)
        {
            await SendFailureAsync(404, "Not found", "NOT_FOUND", "Certificate request not found.", ct);
            return;
        }

        if (request.FeePaid)
        {
            await SendFailureAsync(400, "Fee already paid.", "ALREADY_PAID", "This request is already paid.", ct);
            return;
        }

        var amount = request.FeeAmount ?? 0m;
        if (amount <= 0)
        {
            await SendFailureAsync(400, "No fee required.", "NO_FEE", "No fee is required for this request.", ct);
            return;
        }

        var student = request.Student ?? (request.StudentId != Guid.Empty 
            ? await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StudentId, ct)
            : null);

        var email = !string.IsNullOrWhiteSpace(student?.OfficialEmail) ? student.OfficialEmail :
                    !string.IsNullOrWhiteSpace(student?.PersonalEmail) ? student.PersonalEmail :
                    HttpContext.User?.FindFirst(ClaimTypes.Email)?.Value ??
                    HttpContext.User?.FindFirst("preferred_username")?.Value ??
                    "student@wigweuniversity.edu.ng";

        var callbackUrl = !string.IsNullOrWhiteSpace(req.CallbackUrl)
            ? req.CallbackUrl
            : $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/self-service/certificate";

        var gateway = (req.Gateway ?? "Paystack").Trim();
        var gatewayRef = $"LMS-CR-{request.StudentId.ToString("N")[..8]}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();

        try
        {
            if (gateway.Equals("Hydrogen", StringComparison.OrdinalIgnoreCase))
            {
                var customerName = student != null
                    ? $"{student.FirstName} {student.LastName}".Trim()
                    : "Student";

                var (url, txRef) = await _hydrogenService.InitiatePaymentAsync(
                    email, customerName, amount, "Certificate Request Fee", callbackUrl,
                    new { certificateRequestId = request.Id, system = "LMS" });

                await SendSuccessAsync(new GatewayInitResponse(url, txRef), ct);
            }
            else
            {
                var tuple = await _paystackService.InitializeTransactionAsync(
                    email, amount, gatewayRef, callbackUrl,
                    new { certificateRequestId = request.Id, system = "LMS" });

                await SendSuccessAsync(new GatewayInitResponse(tuple.AuthorizationUrl, tuple.Reference), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize payment gateway ({Gateway}) for certificate request {RequestId}. Email: {Email}", gateway, reqId, email);
            await SendFailureAsync(400, ex.Message, "GATEWAY_ERROR", ex.Message, ct);
        }
    }
}
