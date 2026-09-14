using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Cafeteria;

// ─── Get Balance ────────────────────────────────────────────────────────────

public sealed class GetCafeteriaWalletBalanceEndpoint(ICafeteriaWalletService walletService)
    : ApiEndpointWithoutRequest<CafeteriaWalletBalanceResponse>
{
    public override void Configure()
    {
        Get("cafeteria/Wallet/GetBalance", "cafeteria/wallet/balance");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var requestedUser = Query<string?>("username", isRequired: false);
        var currentUsername = User.Identity?.Name ?? User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;

        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance");
        var targetUsername = (!string.IsNullOrWhiteSpace(requestedUser) && isStaff)
            ? requestedUser
            : (!string.IsNullOrWhiteSpace(currentUsername) ? currentUsername : (requestedUser ?? "student"));

        var response = await walletService.GetBalanceAsync(targetUsername, ct);
        await SendSuccessAsync(response, ct);
    }
}

// ─── Initialize Top-Up ──────────────────────────────────────────────────────

public sealed class InitializeWalletTopUpEndpoint(ICafeteriaWalletService walletService)
    : ApiEndpoint<InitializeWalletTopUpRequest, InitializeWalletTopUpResponse>
{
    public override void Configure()
    {
        Post("cafeteria/Wallet/Initialize", "cafeteria/Wallet/InitializePaystack", "cafeteria/Wallet/InitializeHydrogen");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(InitializeWalletTopUpRequest req, CancellationToken ct)
    {
        try
        {
            var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance");
            var currentUsername = User.Identity?.Name ?? User.FindFirst("name")?.Value;

            var username = (!string.IsNullOrWhiteSpace(req.Username) && isStaff)
                ? req.Username
                : (!string.IsNullOrWhiteSpace(currentUsername) ? currentUsername : (req.Username ?? "student"));

            var updatedReq = req with { Username = username };
            var response = await walletService.InitializeTopUpAsync(updatedReq, ct);
            await SendSuccessAsync(response, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(400, ex.Message, "INITIATION_FAILED", ex.Message, ct);
        }
    }
}

// ─── Verify Payment ─────────────────────────────────────────────────────────

public sealed class VerifyWalletTopUpEndpoint(ICafeteriaWalletService walletService)
    : ApiEndpointWithoutRequest<VerifyWalletTopUpResponse>
{
    public override void Configure()
    {
        Get("cafeteria/Wallet/VerifyPayment", "cafeteria/wallet/verify");
        AllowAnonymous();
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var reference = Query<string?>("reference", isRequired: false);
        if (string.IsNullOrWhiteSpace(reference))
        {
            await SendFailureAsync(400, "Payment reference is required.", "VALIDATION_ERROR", "Payment reference is required.", ct);
            return;
        }

        var username = Query<string?>("username", isRequired: false) ?? User.Identity?.Name ?? "student";
        var gateway = Query<string?>("gateway", isRequired: false) ?? "Paystack";
        var amountStr = Query<string?>("amount", isRequired: false);
        decimal amount = decimal.TryParse(amountStr, out var a) ? a : 0m;

        var req = new VerifyWalletTopUpRequest(reference, username, amount, gateway);
        var response = await walletService.VerifyPaymentAsync(req, ct);

        if (!response.Status)
        {
            await SendFailureAsync(400, response.ResponseMessage, "VERIFICATION_FAILED", response.ResponseMessage, ct);
            return;
        }

        await SendSuccessAsync(response, ct);
    }
}

// ─── Direct Top-Up (Instant / Scholarship / Admin) ──────────────────────────

public sealed class DirectWalletTopUpEndpoint(ICafeteriaWalletService walletService)
    : ApiEndpoint<DirectWalletTopUpRequest, VerifyWalletTopUpResponse>
{
    public override void Configure()
    {
        Post("cafeteria/Wallet/TopUp");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(DirectWalletTopUpRequest req, CancellationToken ct)
    {
        try
        {
            var username = !string.IsNullOrWhiteSpace(req.Username)
                ? req.Username
                : (User.Identity?.Name ?? "student");

            var updatedReq = req with { Username = username };
            var response = await walletService.DirectTopUpAsync(updatedReq, ct);
            await SendSuccessAsync(response, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(400, ex.Message, "TOPUP_FAILED", ex.Message, ct);
        }
    }
}

// ─── Webhooks ───────────────────────────────────────────────────────────────

public sealed class PaystackCafeteriaWebhookEndpoint(ICafeteriaWalletService walletService)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("webhooks/cafeteria/paystack");
        AllowAnonymous();
        Tags("CafeteriaWallet");
        Options(b => b.DisableAntiforgery());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var signature = HttpContext.Request.Headers["x-paystack-signature"].ToString();
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        try
        {
            await walletService.HandlePaystackWebhookAsync(rawBody, signature, ct);
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

public sealed class HydrogenCafeteriaWebhookEndpoint(ICafeteriaWalletService walletService)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("webhooks/cafeteria/hydrogen");
        AllowAnonymous();
        Tags("CafeteriaWallet");
        Options(b => b.DisableAntiforgery());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var signature = HttpContext.Request.Headers["x-hydrogen-signature"].ToString();
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        try
        {
            await walletService.HandleHydrogenWebhookAsync(rawBody, signature, ct);
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
