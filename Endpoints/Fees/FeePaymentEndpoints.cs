using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Enums;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using LMS.Api.Endpoints;

namespace LMS.Api.Endpoints.Fees;

// ─── Initiate gateway payment ─────────────────────────────────────────────────

public sealed class InitiateGatewayPaymentEndpoint(IFeeService feeService)
    : ApiEndpoint<InitiateGatewayPaymentRequest, GatewayInitResponse>
{
    public override void Configure()
    {
        Post("/api/fees/payments/initiate");
        Roles("SuperAdmin", "Admin", "Finance", "Student");
    }

    public override async Task HandleAsync(InitiateGatewayPaymentRequest req, CancellationToken ct)
    {
        try
        {
            var result = await feeService.InitiateGatewayPaymentAsync(req);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_GATEWAY", ex.Message, ct);
        }
    }
}

// ─── Record manual payment (with optional receipt file) ───────────────────────

public sealed class RecordManualPaymentEndpoint(IFeeService feeService)
    : ApiEndpoint<RecordManualPaymentRequest, FeePaymentResponse>
{
    public override void Configure()
    {
        Post("/api/fees/payments/manual");
        AllowFileUploads();
        Roles("SuperAdmin", "Admin", "Finance", "Student");
    }

    public override async Task HandleAsync(RecordManualPaymentRequest req, CancellationToken ct)
    {
        string? receiptUrl = null;

        // If a receipt file was uploaded, save it (simplified: store in wwwroot/receipts)
        if (Files.Count > 0)
        {
            var file = Files[0];
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "receipts");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream, ct);
            receiptUrl = $"/receipts/{fileName}";
        }

        var payment = await feeService.RecordManualPaymentAsync(req, receiptUrl);
        await SendSuccessAsync(FeeMapper.ToPaymentResponse(payment), ct);
    }
}

// ─── Confirm / Reject ─────────────────────────────────────────────────────────

public sealed class ConfirmPaymentEndpoint(IFeeService feeService)
    : ApiEndpoint<ConfirmPaymentRequest, FeePaymentResponse>
{
    public override void Configure()
    {
        Patch("/api/fees/payments/{id}/confirm");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(ConfirmPaymentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var confirmedBy = User.Identity?.Name ?? "Admin";
        try
        {
            var payment = await feeService.ConfirmPaymentAsync(id, confirmedBy);
            await SendSuccessAsync(FeeMapper.ToPaymentResponse(payment), ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_STATE", ex.Message, ct);
        }
    }
}

public sealed class RejectPaymentEndpoint(IFeeService feeService)
    : ApiEndpoint<RejectPaymentRequest, FeePaymentResponse>
{
    public override void Configure()
    {
        Patch("/api/fees/payments/{id}/reject");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(RejectPaymentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var payment = await feeService.RejectPaymentAsync(id, req.RejectionReason);
            await SendSuccessAsync(FeeMapper.ToPaymentResponse(payment), ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_STATE", ex.Message, ct);
        }
    }
}

// ─── Payment History ──────────────────────────────────────────────────────────

public sealed class GetPaymentHistoryEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<IEnumerable<FeePaymentResponse>>
{
    public override void Configure()
    {
        Get("/api/fees/payments/student/{studentId}");
        Roles("SuperAdmin", "Admin", "Finance", "Student", "Registry");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = Route<Guid>("studentId");

        // Ownership check: students can only access their own payment history
        if (User.IsInRole("Student") &&
            !User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
            !User.IsInRole("Finance") && !User.IsInRole("Registry"))
        {
            var callerId = HttpContext.Items["CurrentUserId"] as Guid?;
            if (callerId != studentId)
            {
                await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You can only access your own payment history.", ct);
                return;
            }
        }

        var payments = await feeService.GetPaymentHistoryAsync(studentId);
        await SendSuccessAsync(payments.Select(FeeMapper.ToPaymentResponse), ct);
    }
}

public sealed class GetAllPaymentsEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<IEnumerable<FeePaymentResponse>>
{
    public override void Configure()
    {
        Get("/api/fees/payments");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var statusStr = Query<string?>("status", isRequired: false);
        var sessionIdStr = Query<string?>("sessionId", isRequired: false);
        Data.Enums.PaymentStatus? status = statusStr != null && Enum.TryParse<Data.Enums.PaymentStatus>(statusStr, true, out var s) ? s : null;
        Guid? sessionId = sessionIdStr != null && Guid.TryParse(sessionIdStr, out var g) ? g : null;
        var payments = await feeService.GetAllPaymentsAsync(status, sessionId);
        await SendSuccessAsync(payments.Select(FeeMapper.ToPaymentResponse), ct);
    }
}

