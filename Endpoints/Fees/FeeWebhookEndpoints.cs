using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Fees;

// ─── Apply Late Fees (admin-triggered) ───────────────────────────────────────

public sealed class ApplyLateFeesEndpoint(IFeeService feeService)
    : ApiEndpoint<ApplyLateFeesRequest, IEnumerable<ApplyLateFeesResult>>
{
    public override void Configure()
    {
        Post("/api/fees/late-fees/apply");
        Roles("SuperAdmin", "Admin", "Finance");
    }

    public override async Task HandleAsync(ApplyLateFeesRequest req, CancellationToken ct)
    {
        var appliedBy = User.Identity?.Name ?? "Admin";
        var results = await feeService.ApplyLateFeesAsync(req.SessionId, req.IsDryRun, appliedBy);
        await SendSuccessAsync(results, ct);
    }
}

// ─── Paystack Webhook ─────────────────────────────────────────────────────────

public sealed class PaystackWebhookEndpoint(IFeeService feeService)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/webhooks/paystack");
        AllowAnonymous();
        // Must read raw body for HMAC verification
        Options(b => b.DisableAntiforgery());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var signature = HttpContext.Request.Headers["x-paystack-signature"].ToString();
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        try
        {
            await feeService.HandlePaystackWebhookAsync(rawBody, signature);
            await Send.OkAsync(ct); // Paystack requires 200 OK
        }
        catch (UnauthorizedAccessException)
        {
            await Send.ForbiddenAsync(ct);
        }
        catch
        {
            await Send.OkAsync(ct); // Always return 200 to prevent retries for non-signature issues
        }
    }
}

// ─── Hydrogen Webhook ─────────────────────────────────────────────────────────

public sealed class HydrogenWebhookEndpoint(IFeeService feeService)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/webhooks/hydrogen");
        AllowAnonymous();
        Options(b => b.DisableAntiforgery());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var signature = HttpContext.Request.Headers["x-hydrogen-signature"].ToString();
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        try
        {
            await feeService.HandleHydrogenWebhookAsync(rawBody, signature);
            await Send.OkAsync(ct);
        }
        catch (UnauthorizedAccessException)
        {
            await Send.ForbiddenAsync(ct);
        }
        catch
        {
            await Send.OkAsync(ct);
        }
    }
}
