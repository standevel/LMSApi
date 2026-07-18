using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Reporting;

public class InitiateTranscriptPaymentRequest
{
    public Guid RequestId { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
}

public sealed class InitiateTranscriptPaymentEndpoint : ApiEndpoint<InitiateTranscriptPaymentRequest, GatewayInitResponse>
{
    private readonly LmsDbContext _db;
    private readonly PaystackService _paystackService;

    public InitiateTranscriptPaymentEndpoint(LmsDbContext db, PaystackService paystackService)
    {
        _db = db;
        _paystackService = paystackService;
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

        var request = await _db.TranscriptRequests.Include(tr => tr.Student).FirstOrDefaultAsync(tr => tr.Id == req.RequestId, ct);
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

        var gatewayRef = $"LMS-TR-{request.StudentId.ToString("N")[..8]}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
        var email = request.Student?.Email ?? "student@wigwe.edu.ng";

        var tuple = await _paystackService.InitializeTransactionAsync(
            email, amount, gatewayRef, req.CallbackUrl,
            new { transcriptRequestId = request.Id, system = "LMS" });

        await SendSuccessAsync(new GatewayInitResponse(tuple.AuthorizationUrl, tuple.Reference), ct);
    }
}

public class InitiateCertificatePaymentRequest
{
    public Guid RequestId { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
}

public sealed class InitiateCertificatePaymentEndpoint : ApiEndpoint<InitiateCertificatePaymentRequest, GatewayInitResponse>
{
    private readonly LmsDbContext _db;
    private readonly PaystackService _paystackService;

    public InitiateCertificatePaymentEndpoint(LmsDbContext db, PaystackService paystackService)
    {
        _db = db;
        _paystackService = paystackService;
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

        var request = await _db.CertificateRequests.Include(cr => cr.Student).FirstOrDefaultAsync(cr => cr.Id == req.RequestId, ct);
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

        var gatewayRef = $"LMS-CR-{request.StudentId.ToString("N")[..8]}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
        var email = request.Student?.OfficialEmail ?? request.Student?.PersonalEmail ?? "student@wigwe.edu.ng";

        var tuple = await _paystackService.InitializeTransactionAsync(
            email, amount, gatewayRef, req.CallbackUrl,
            new { certificateRequestId = request.Id, system = "LMS" });

        await SendSuccessAsync(new GatewayInitResponse(tuple.AuthorizationUrl, tuple.Reference), ct);
    }
}
