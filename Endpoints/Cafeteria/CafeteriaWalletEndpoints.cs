using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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

public sealed class InitializeWalletTopUpEndpoint(ICafeteriaWalletService walletService, LmsDbContext dbContext)
    : ApiEndpoint<InitializeWalletTopUpRequest, InitializeWalletTopUpResponse>
{
    public override void Configure()
    {
        Post("cafeteria/Wallet/Initialize",
             "cafeteria/Wallet/InitializePaystack",
             "cafeteria/Wallet/InitializeHydrogen",
             "cafeteria/wallet/initialize-topup",
             "cafeteria/Wallet/Initialize-TopUp");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(InitializeWalletTopUpRequest req, CancellationToken ct)
    {
        try
        {
            // Authorization is derived solely from authenticated role claims; never from the client-supplied PayerRole.
            var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance");
            var isParent = User.IsInRole("Parent");
            var isStudent = !(isStaff || isParent);

            var currentUsername = User.Identity?.Name
                ?? User.FindFirst("name")?.Value
                ?? User.FindFirst("preferred_username")?.Value;

            var config = await dbContext.SystemCafeteriaConfigurations.FirstOrDefaultAsync(ct)
                ?? new SystemCafeteriaConfiguration();

            // Enforce kill switches + master flag for all self-service (non-staff) paths.
            if (!isStaff)
            {
                if (!config.EnableSelfServiceTopUp)
                {
                    await SendFailureAsync(403, "Online cafeteria wallet top-up is currently disabled by university administration.", "TOPUP_DISABLED", "Online cafeteria wallet top-up is disabled.", ct);
                    return;
                }

                if (isParent && !config.AllowParentTopUp)
                {
                    await SendFailureAsync(403, "Parent wallet top-up is currently disabled by university administration.", "PARENT_TOPUP_DISABLED", "Parent wallet top-up is disabled.", ct);
                    return;
                }

                if (isStudent && !config.AllowStudentSelfTopUp)
                {
                    await SendFailureAsync(403, "Student self-service wallet top-up is currently disabled by university administration.", "STUDENT_TOPUP_DISABLED", "Student wallet top-up is disabled.", ct);
                    return;
                }
            }

            string username;
            if (isStaff)
            {
                // Staff may credit any account; username resolved from explicit target when provided.
                username = !string.IsNullOrWhiteSpace(req.TargetUsername) ? req.TargetUsername : req.Username;
            }
            else if (isParent)
            {
                // Parents may only top up their own verified child accounts.
                var target = (!string.IsNullOrWhiteSpace(req.TargetUsername) ? req.TargetUsername : req.Username) ?? string.Empty;
                var cleanTarget = target.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(cleanTarget))
                {
                    await SendFailureAsync(400, "A target username (your child) is required for parent top-up.", "TARGET_REQUIRED", "Provide the target username for your child.", ct);
                    return;
                }

                var authorized = await IsParentOfAsync(currentUsername, cleanTarget, dbContext, ct);
                if (!authorized)
                {
                    await SendFailureAsync(403, "You may only top up wallets for your own linked children.", "NOT_YOUR_CHILD", "Parent-child relationship not verified.", ct);
                    return;
                }
                username = cleanTarget;
            }
            else
            {
                // Students may only top up their own account; ignore any client-supplied target.
                username = currentUsername ?? req.Username ?? "student";
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                await SendFailureAsync(401, "Unable to resolve an authenticated username.", "UNAUTHENTICATED", "An authenticated username is required.", ct);
                return;
            }

            var updatedReq = req with { Username = username };
            // The wallet service enforces MaxSingleTopUpAmount server-side.
            var response = await walletService.InitializeTopUpAsync(updatedReq, ct);
            await SendSuccessAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(403, ex.Message, "LIMIT_EXCEEDED", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(400, ex.Message, "INITIATION_FAILED", ex.Message, ct);
        }
    }

    private static async Task<bool> IsParentOfAsync(string? parentUsername, string childUsername, LmsDbContext dbContext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parentUsername)) return false;
        var cleanParent = parentUsername.Trim().ToLowerInvariant();

        var userId = await dbContext.Users
            .Where(u => (u.Email != null && u.Email.ToLower() == cleanParent) || (u.Username != null && u.Username.ToLower() == cleanParent))
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == Guid.Empty) return false;

        return await dbContext.ParentStudentLinks
            .Where(l => l.ParentGuardian != null && l.ParentGuardian.UserId == userId)
            .SelectMany(l => dbContext.Students.Where(s => s.Id == l.StudentId))
            .AnyAsync(s => s.OfficialEmail.ToLower() == childUsername || (s.StudentNumber != null && s.StudentNumber.ToLower() == childUsername), ct);
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

// ─── Pay With Wallet (meal purchase) ─────────────────────────────────────────

public sealed class PayWithWalletEndpoint(ICafeteriaWalletService walletService)
    : ApiEndpoint<PayWithWalletRequest, PayWithWalletResponse>
{
    public override void Configure()
    {
        Post("cafeteria/Wallet/PayWithWallet", "cafeteria/wallet/pay");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(PayWithWalletRequest req, CancellationToken ct)
    {
        var currentUsername = User.Identity?.Name
            ?? User.FindFirst("name")?.Value
            ?? User.FindFirst("preferred_username")?.Value;

        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("Finance");
        var username = isStaff
            ? (!string.IsNullOrWhiteSpace(req.Username) ? req.Username : (currentUsername ?? "student"))
            : (currentUsername ?? req.Username ?? "student");

        var response = await walletService.PayWithWalletAsync(req with { Username = username }, ct);
        if (!response.Status)
        {
            await SendFailureAsync(402, response.Message, "PAYMENT_FAILED", response.Message, ct);
            return;
        }

        await SendSuccessAsync(response, ct);
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

// ─── System Cafeteria Configuration ─────────────────────────────────────────

public sealed class GetSystemCafeteriaConfigurationEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<SystemCafeteriaConfigurationDto>
{
    public override void Configure()
    {
        Get("cafeteria/Config", "cafeteria/configuration");
        AllowAnonymous();
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var config = await dbContext.SystemCafeteriaConfigurations.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new SystemCafeteriaConfiguration();
            dbContext.SystemCafeteriaConfigurations.Add(config);
            await dbContext.SaveChangesAsync(ct);
        }

        await SendSuccessAsync(new SystemCafeteriaConfigurationDto(
            config.Id,
            config.EnableSelfServiceTopUp,
            config.AllowStudentSelfTopUp,
            config.AllowParentTopUp,
            config.MaxSingleTopUpAmount,
            config.DailySpendLimit,
            config.UpdatedAt,
            config.EnforceMealSessionWindows,
            config.AllowPreOrdersOutsideWindows
        ), ct);
    }
}

public sealed class UpdateSystemCafeteriaConfigurationEndpoint(LmsDbContext dbContext)
    : ApiEndpoint<UpdateSystemCafeteriaConfigurationRequest, SystemCafeteriaConfigurationDto>
{
    public override void Configure()
    {
        Post("cafeteria/Config");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(UpdateSystemCafeteriaConfigurationRequest req, CancellationToken ct)
    {
        var config = await dbContext.SystemCafeteriaConfigurations.FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = new SystemCafeteriaConfiguration();
            dbContext.SystemCafeteriaConfigurations.Add(config);
        }

        config.EnableSelfServiceTopUp = req.EnableSelfServiceTopUp;
        config.AllowStudentSelfTopUp  = req.AllowStudentSelfTopUp;
        config.AllowParentTopUp       = req.AllowParentTopUp;
        config.MaxSingleTopUpAmount   = req.MaxSingleTopUpAmount;
        config.DailySpendLimit        = req.DailySpendLimit;
        config.EnforceMealSessionWindows = req.EnforceMealSessionWindows;
        config.AllowPreOrdersOutsideWindows = req.AllowPreOrdersOutsideWindows;
        config.UpdatedAt              = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new SystemCafeteriaConfigurationDto(
            config.Id,
            config.EnableSelfServiceTopUp,
            config.AllowStudentSelfTopUp,
            config.AllowParentTopUp,
            config.MaxSingleTopUpAmount,
            config.DailySpendLimit,
            config.UpdatedAt,
            config.EnforceMealSessionWindows,
            config.AllowPreOrdersOutsideWindows
        ), ct);
    }
}

// ─── Parent Delegated Policy Endpoints ─────────────────────────────────────

public static class CafeteriaParentSecurityHelper
{
    public static async Task<bool> IsParentOfAsync(string? parentUsername, string childUsername, LmsDbContext dbContext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parentUsername)) return false;
        var cleanParent = parentUsername.Trim().ToLowerInvariant();

        var userId = await dbContext.Users
            .Where(u => (u.Email != null && u.Email.ToLower() == cleanParent) || (u.Username != null && u.Username.ToLower() == cleanParent))
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId == Guid.Empty) return false;

        return await dbContext.ParentStudentLinks
            .Where(l => l.ParentGuardian != null && l.ParentGuardian.UserId == userId)
            .SelectMany(l => dbContext.Students.Where(s => s.Id == l.StudentId))
            .AnyAsync(s => s.OfficialEmail.ToLower() == childUsername || (s.StudentNumber != null && s.StudentNumber.ToLower() == childUsername), ct);
    }
}

public sealed class GetParentStudentPolicyEndpoint(ICafeteriaWalletService walletService, LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<ParentStudentCafeteriaPolicyDto>
{
    public override void Configure()
    {
        Get("cafeteria/parent/policy/{studentUsername}");
        Roles("Parent", "SuperAdmin", "Admin");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentUsername = Route<string>("studentUsername");
        var currentUsername = User.Identity?.Name ?? User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;
        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var cleanChild = (studentUsername ?? string.Empty).Trim().ToLowerInvariant();
        if (!isStaff)
        {
            var isParent = await CafeteriaParentSecurityHelper.IsParentOfAsync(currentUsername, cleanChild, dbContext, ct);
            if (!isParent)
            {
                await SendFailureAsync(403, "You may only view dining policies for your own linked children.", "FORBIDDEN", "Relationship not verified.", ct);
                return;
            }
        }

        var balance = await walletService.GetBalanceAsync(cleanChild, ct);
        var config = await walletService.GetConfigurationAsync(ct);
        var spentToday = await walletService.GetDailySpendAsync(cleanChild, ct);

        var account = await dbContext.CafeteriaWalletAccounts.FirstOrDefaultAsync(a => a.Username.ToLower() == cleanChild, ct);
        var parentLimit = account?.ParentDailySpendLimit;

        decimal effectiveLimit = config.DailySpendLimit;
        if (parentLimit.HasValue && parentLimit.Value > 0)
        {
            effectiveLimit = config.DailySpendLimit > 0
                ? Math.Min(config.DailySpendLimit, parentLimit.Value)
                : parentLimit.Value;
        }

        await SendSuccessAsync(new ParentStudentCafeteriaPolicyDto(
            StudentUsername: cleanChild,
            StudentName: balance.FullName,
            WalletBalance: balance.WalletBalance,
            ParentDailySpendLimit: parentLimit,
            SystemDailySpendLimit: config.DailySpendLimit,
            EffectiveDailySpendLimit: effectiveLimit,
            SpentToday: spentToday
        ), ct);
    }
}

public sealed class SetParentDailyLimitEndpoint(ICafeteriaWalletService walletService, LmsDbContext dbContext)
    : ApiEndpoint<SetParentDailyLimitRequest, SetParentDailyLimitResponse>
{
    public override void Configure()
    {
        Post("cafeteria/parent/set-daily-limit");
        Roles("Parent", "SuperAdmin", "Admin");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(SetParentDailyLimitRequest req, CancellationToken ct)
    {
        var currentUsername = User.Identity?.Name ?? User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;
        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var cleanChild = (req.StudentUsername ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanChild))
        {
            await SendFailureAsync(400, "Child username is required.", "INVALID_REQUEST", "Child username is required.", ct);
            return;
        }

        if (req.DailyLimit.HasValue && req.DailyLimit.Value < 0)
        {
            await SendFailureAsync(400, "Daily spending limit cannot be negative.", "INVALID_LIMIT", "Limit must be >= 0.", ct);
            return;
        }

        if (!isStaff)
        {
            var isParent = await CafeteriaParentSecurityHelper.IsParentOfAsync(currentUsername, cleanChild, dbContext, ct);
            if (!isParent)
            {
                await SendFailureAsync(403, "You may only adjust spending limits for your own linked children.", "FORBIDDEN", "Relationship not verified.", ct);
                return;
            }
        }

        await walletService.EnsureAccountExistsAsync(cleanChild, ct);
        var account = await dbContext.CafeteriaWalletAccounts.FirstOrDefaultAsync(a => a.Username.ToLower() == cleanChild, ct);
        if (account is null)
        {
            await SendFailureAsync(404, "Student wallet account not found.", "NOT_FOUND", "Account not found.", ct);
            return;
        }

        account.ParentDailySpendLimit = req.DailyLimit;
        account.ConcurrencyToken = Guid.NewGuid();
        account.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var config = await walletService.GetConfigurationAsync(ct);
        decimal effectiveLimit = config.DailySpendLimit;
        if (account.ParentDailySpendLimit.HasValue && account.ParentDailySpendLimit.Value > 0)
        {
            effectiveLimit = config.DailySpendLimit > 0
                ? Math.Min(config.DailySpendLimit, account.ParentDailySpendLimit.Value)
                : account.ParentDailySpendLimit.Value;
        }

        string msg = req.DailyLimit.HasValue && req.DailyLimit.Value > 0
            ? $"Daily spending limit updated to ₦{req.DailyLimit.Value:N2}."
            : "Custom daily limit removed. Campus default limit applies.";

        await SendSuccessAsync(new SetParentDailyLimitResponse(
            Success: true,
            Message: msg,
            ParentDailySpendLimit: account.ParentDailySpendLimit,
            EffectiveDailySpendLimit: effectiveLimit
        ), ct);
    }
}

public sealed class GetParentChildMealHistoryEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<List<ParentChildMealOrderDto>>
{
    public override void Configure()
    {
        Get("cafeteria/parent/child-orders/{studentUsername}");
        Roles("Parent", "SuperAdmin", "Admin");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var currentUsername = User.Identity?.Name ?? User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;
        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var childUsername = Route<string>("studentUsername");
        var cleanChild = (childUsername ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(cleanChild))
        {
            await SendFailureAsync(400, "Student username is required.", "INVALID_REQUEST", "Student username is required.", ct);
            return;
        }

        if (!isStaff)
        {
            var isParent = await CafeteriaParentSecurityHelper.IsParentOfAsync(currentUsername, cleanChild, dbContext, ct);
            if (!isParent)
            {
                await SendFailureAsync(403, "You may only view dining activity for your own linked children.", "FORBIDDEN", "Relationship not verified.", ct);
                return;
            }
        }

        var orders = await dbContext.CafeteriaVendorOrders
            .Where(o => o.StudentUsername.ToLower() == cleanChild)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .Select(o => new ParentChildMealOrderDto(
                o.Id,
                o.OrderCode,
                o.MenuItemName,
                o.ImageUrl,
                o.Price,
                o.IsScholarshipCovered,
                o.VendorName,
                o.Status.ToString(),
                o.CreatedAt,
                o.ClaimedAt))
            .ToListAsync(ct);

        await SendSuccessAsync(orders, ct);
    }
}

public sealed class GetParentChildWalletTransactionsEndpoint(LmsDbContext dbContext)
    : ApiEndpointWithoutRequest<List<CafeteriaWalletTransactionDto>>
{
    public override void Configure()
    {
        Get("cafeteria/parent/child-transactions/{studentUsername}");
        Roles("Parent", "SuperAdmin", "Admin");
        Tags("CafeteriaWallet");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var currentUsername = User.Identity?.Name ?? User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;
        var isStaff = User.IsInRole("SuperAdmin") || User.IsInRole("Admin");

        var childUsername = Route<string>("studentUsername");
        var cleanChild = (childUsername ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(cleanChild))
        {
            await SendFailureAsync(400, "Student username is required.", "INVALID_REQUEST", "Student username is required.", ct);
            return;
        }

        if (!isStaff)
        {
            var isParent = await CafeteriaParentSecurityHelper.IsParentOfAsync(currentUsername, cleanChild, dbContext, ct);
            if (!isParent)
            {
                await SendFailureAsync(403, "You may only view ledger activity for your own linked children.", "FORBIDDEN", "Relationship not verified.", ct);
                return;
            }
        }

        var txs = await dbContext.CafeteriaWalletTransactions
            .Where(t => t.WalletAccount.Username.ToLower() == cleanChild)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new CafeteriaWalletTransactionDto(
                t.Id,
                t.Amount,
                t.TransactionType,
                t.Gateway,
                t.Reference,
                t.Status,
                t.Description,
                t.BalanceAfter,
                t.CreatedAt,
                t.VerifiedAt))
            .ToListAsync(ct);

        await SendSuccessAsync(txs, ct);
    }
}

