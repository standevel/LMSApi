using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public sealed class CafeteriaWalletService : ICafeteriaWalletService
{
    private readonly LmsDbContext _db;
    private readonly PaystackService _paystackService;
    private readonly HydrogenService _hydrogenService;
    private readonly ILogger<CafeteriaWalletService> _logger;

    public CafeteriaWalletService(
        LmsDbContext db,
        PaystackService paystackService,
        HydrogenService hydrogenService,
        ILogger<CafeteriaWalletService> logger)
    {
        _db = db;
        _paystackService = paystackService;
        _hydrogenService = hydrogenService;
        _logger = logger;
    }

    private async Task<CafeteriaWalletAccount> GetOrCreateAccountAsync(string username, CancellationToken ct = default)
    {
        var cleanUsername = (username ?? "student").Trim().ToLowerInvariant();

        var account = await _db.CafeteriaWalletAccounts
            .Include(a => a.User)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Username.ToLower() == cleanUsername, ct);

        if (account == null)
        {
            // Try to match AppUser or Student
            var user = await _db.Users
                .FirstOrDefaultAsync(u => (u.Email != null && u.Email.ToLower() == cleanUsername) || (u.Username != null && u.Username.ToLower() == cleanUsername), ct);

            Student? student = null;
            if (user != null)
            {
                student = await _db.Students
                    .FirstOrDefaultAsync(s => (user.Email != null && s.OfficialEmail.ToLower() == user.Email.ToLower()) || (user.EntraObjectId != null && s.EntraObjectId == user.EntraObjectId), ct);
            }
            else
            {
                student = await _db.Students
                    .FirstOrDefaultAsync(s => s.OfficialEmail.ToLower() == cleanUsername || (s.StudentNumber != null && s.StudentNumber.ToLower() == cleanUsername), ct);
            }

            account = new CafeteriaWalletAccount
            {
                Username = username ?? "student",
                UserId = user?.Id,
                StudentId = student?.Id,
                Balance = 0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.CafeteriaWalletAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
        }

        return account;
    }

    public async Task<CafeteriaWalletBalanceResponse> GetBalanceAsync(string username, CancellationToken ct = default)
    {
        var account = await GetOrCreateAccountAsync(username, ct);

        var transactions = await _db.CafeteriaWalletTransactions
            .Where(t => t.WalletAccountId == account.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
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
                t.VerifiedAt
            ))
            .ToListAsync(ct);

        string fullName = account.Student != null
            ? $"{account.Student.FirstName} {account.Student.LastName}"
            : (account.User?.DisplayName ?? account.Username);

        return new CafeteriaWalletBalanceResponse(
            account.Id,
            account.UserId,
            account.Username,
            fullName,
            account.Balance,
            transactions
        );
    }

    public async Task<InitializeWalletTopUpResponse> InitializeTopUpAsync(InitializeWalletTopUpRequest req, CancellationToken ct = default)
    {
        if (req.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var config = await GetConfigurationAsync(ct);
        if (req.Amount > config.MaxSingleTopUpAmount)
        {
            throw new InvalidOperationException(
                $"Requested top-up amount {req.Amount:N2} exceeds the maximum single top-up of {config.MaxSingleTopUpAmount:N2} NGN.");
        }

        var account = await GetOrCreateAccountAsync(req.Username, ct);
        string gateway = (req.Gateway ?? "Paystack").Trim();
        string email = account.User?.Email ?? (account.Username.Contains('@') ? account.Username : "student@wigweuniversity.edu.ng");
        string callbackUrl = req.CallbackUrl ?? "http://localhost:4200/dashboard/student/cafeteria";

        string authUrl;
        string reference;

        if (gateway.Equals("Hydrogen", StringComparison.OrdinalIgnoreCase))
        {
            string customerName = account.Student != null
                ? $"{account.Student.FirstName} {account.Student.LastName}"
                : (account.User?.DisplayName ?? account.Username);

            var (hUrl, hRef) = await _hydrogenService.InitiatePaymentAsync(
                email,
                customerName,
                req.Amount,
                $"Cafeteria Wallet Top-up: {req.Amount:N2} NGN",
                callbackUrl,
                new { accountId = account.Id, username = req.Username, type = "CafeteriaTopUp" }
            );
            authUrl = hUrl;
            reference = hRef;
        }
        else
        {
            string generatedRef = $"WUC-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
            var (pUrl, pRef) = await _paystackService.InitializeTransactionAsync(
                email,
                req.Amount,
                generatedRef,
                callbackUrl,
                new { accountId = account.Id, username = req.Username, type = "CafeteriaTopUp" }
            );
            authUrl = pUrl;
            reference = pRef;
        }

        // Record pending transaction in DB
        var tx = new CafeteriaWalletTransaction
        {
            WalletAccountId = account.Id,
            Amount = req.Amount,
            TransactionType = "Credit",
            Gateway = gateway,
            Reference = reference,
            Status = "Pending",
            Description = $"Wallet Top-up via {gateway}",
            BalanceAfter = account.Balance,
            CreatedAt = DateTime.UtcNow
        };

        _db.CafeteriaWalletTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        return new InitializeWalletTopUpResponse(
            Status: true,
            Message: $"{gateway} checkout initialized.",
            AuthorizationUrl: authUrl,
            Reference: reference,
            Amount: req.Amount
        );
    }

    public async Task<VerifyWalletTopUpResponse> VerifyPaymentAsync(VerifyWalletTopUpRequest req, CancellationToken ct = default)
    {
        var account = await GetOrCreateAccountAsync(req.Username, ct);
        var tx = await _db.CafeteriaWalletTransactions
            .FirstOrDefaultAsync(t => t.Reference == req.Reference, ct);

        // Idempotency: If already credited, return current balance immediately
        if (tx != null && tx.Status.Equals("Successful", StringComparison.OrdinalIgnoreCase))
        {
            return new VerifyWalletTopUpResponse(
                Status: true,
                ResponseCode: "00",
                ResponseMessage: "Transaction was already verified and credited.",
                NewBalance: account.Balance,
                PaymentReference: req.Reference,
                Gateway: tx.Gateway
            );
        }

        string gateway = tx?.Gateway ?? req.Gateway ?? "Paystack";
        bool isSuccess = false;
        decimal verifiedAmount = tx?.Amount ?? req.Amount;
        string verifyMsg = "";

        if (gateway.Equals("Hydrogen", StringComparison.OrdinalIgnoreCase))
        {
            var result = await _hydrogenService.VerifyPaymentAsync(req.Reference);
            isSuccess = result.IsSuccessful;
            if (result.AmountNaira > 0) verifiedAmount = result.AmountNaira;
            verifyMsg = result.Message;
        }
        else
        {
            var result = await _paystackService.VerifyTransactionAsync(req.Reference);
            isSuccess = result.IsSuccessful;
            if (result.AmountNaira > 0) verifiedAmount = result.AmountNaira;
            verifyMsg = result.Message;
        }

        if (!isSuccess)
        {
            if (tx != null)
            {
                tx.Status = "Failed";
                await _db.SaveChangesAsync(ct);
            }

            return new VerifyWalletTopUpResponse(
                Status: false,
                ResponseCode: "99",
                ResponseMessage: string.IsNullOrWhiteSpace(verifyMsg) ? "Payment verification failed with gateway." : verifyMsg,
                NewBalance: account.Balance,
                PaymentReference: req.Reference,
                Gateway: gateway
            );
        }

        // Verification succeeded: atomically update account balance
        account.Balance += verifiedAmount;
        account.ConcurrencyToken = Guid.NewGuid();
        account.UpdatedAt = DateTime.UtcNow;

        if (tx == null)
        {
            tx = new CafeteriaWalletTransaction
            {
                WalletAccountId = account.Id,
                Amount = verifiedAmount,
                TransactionType = "Credit",
                Gateway = gateway,
                Reference = req.Reference,
                Status = "Successful",
                Description = $"Verified Wallet Top-up via {gateway}",
                BalanceAfter = account.Balance,
                CreatedAt = DateTime.UtcNow,
                VerifiedAt = DateTime.UtcNow
            };
            _db.CafeteriaWalletTransactions.Add(tx);
        }
        else
        {
            tx.Status = "Successful";
            tx.Amount = verifiedAmount;
            tx.BalanceAfter = account.Balance;
            tx.VerifiedAt = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency token conflict during wallet verification for reference {Reference}. Checking idempotency state.", req.Reference);
            _db.ChangeTracker.Clear();
            var reloadedTx = await _db.CafeteriaWalletTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Reference == req.Reference, ct);

            if (reloadedTx != null && reloadedTx.Status.Equals("Successful", StringComparison.OrdinalIgnoreCase))
            {
                var reloadedAcc = await _db.CafeteriaWalletAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Username.ToLower() == req.Username.ToLower(), ct);

                return new VerifyWalletTopUpResponse(
                    Status: true,
                    ResponseCode: "00",
                    ResponseMessage: "Transaction was already verified and credited.",
                    NewBalance: reloadedAcc?.Balance ?? account.Balance,
                    PaymentReference: req.Reference,
                    Gateway: gateway
                );
            }
            throw;
        }

        _logger.LogInformation("Successfully credited Cafeteria Wallet for {Username} with {Amount} NGN via {Gateway}. Reference: {Reference}. New Balance: {Balance}",
            account.Username, verifiedAmount, gateway, req.Reference, account.Balance);

        return new VerifyWalletTopUpResponse(
            Status: true,
            ResponseCode: "00",
            ResponseMessage: $"Payment verified! ₦{verifiedAmount:N2} credited to cafeteria wallet.",
            NewBalance: account.Balance,
            PaymentReference: req.Reference,
            Gateway: gateway
        );
    }

    public async Task<VerifyWalletTopUpResponse> DirectTopUpAsync(DirectWalletTopUpRequest req, CancellationToken ct = default)
    {
        if (req.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        var account = await GetOrCreateAccountAsync(req.Username, ct);
        account.Balance += req.Amount;
        account.ConcurrencyToken = Guid.NewGuid();
        account.UpdatedAt = DateTime.UtcNow;

        string reference = string.IsNullOrWhiteSpace(req.PaymentReference)
            ? $"DIR-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}"
            : req.PaymentReference;

        var tx = new CafeteriaWalletTransaction
        {
            WalletAccountId = account.Id,
            Amount = req.Amount,
            TransactionType = "Credit",
            Gateway = req.PaymentChannel ?? "Direct Instant",
            Reference = reference,
            Status = "Successful",
            Description = req.Description ?? "Direct Wallet Credit",
            BalanceAfter = account.Balance,
            CreatedAt = DateTime.UtcNow,
            VerifiedAt = DateTime.UtcNow
        };

        _db.CafeteriaWalletTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        return new VerifyWalletTopUpResponse(
            Status: true,
            ResponseCode: "00",
            ResponseMessage: $"Direct credit successful! ₦{req.Amount:N2} added to wallet.",
            NewBalance: account.Balance,
            PaymentReference: reference,
            Gateway: req.PaymentChannel ?? "Direct Instant"
        );
    }

    public async Task HandlePaystackWebhookAsync(string rawBody, string signature, CancellationToken ct = default)
    {
        if (!_paystackService.VerifySignature(rawBody, signature))
        {
            _logger.LogWarning("Invalid Paystack webhook signature for Cafeteria Wallet.");
            throw new UnauthorizedAccessException("Invalid Paystack signature.");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        string eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "" : "";

        if (eventType.Equals("charge.success", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("data", out var data))
        {
            string reference = data.TryGetProperty("reference", out var rf) ? rf.GetString() ?? "" : "";
            long amountKobo = data.TryGetProperty("amount", out var am) ? am.GetInt64() : 0;
            decimal amountNaira = (decimal)amountKobo / 100m;

            var tx = await _db.CafeteriaWalletTransactions
                .Include(t => t.WalletAccount)
                .FirstOrDefaultAsync(t => t.Reference == reference, ct);

            if (tx != null && !tx.Status.Equals("Successful", StringComparison.OrdinalIgnoreCase))
            {
                tx.WalletAccount.Balance += amountNaira;
                tx.WalletAccount.ConcurrencyToken = Guid.NewGuid();
                tx.WalletAccount.UpdatedAt = DateTime.UtcNow;
                tx.Status = "Successful";
                tx.BalanceAfter = tx.WalletAccount.Balance;
                tx.VerifiedAt = DateTime.UtcNow;
                try
                {
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Webhook asynchronously credited cafeteria wallet. Ref: {Ref}, Amount: {Amount}", reference, amountNaira);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogInformation("Webhook encountered concurrency token update for Ref: {Ref}; already verified concurrently.", reference);
                }
            }
        }
    }

    public async Task HandleHydrogenWebhookAsync(string rawBody, string signature, CancellationToken ct = default)
    {
        if (!_hydrogenService.VerifySignature(rawBody, signature))
        {
            _logger.LogWarning("Invalid Hydrogen webhook signature for Cafeteria Wallet.");
            throw new UnauthorizedAccessException("Invalid Hydrogen signature.");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        string status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";

        if ((status.Equals("Paid", StringComparison.OrdinalIgnoreCase) || status.Equals("success", StringComparison.OrdinalIgnoreCase))
            && root.TryGetProperty("data", out var data))
        {
            string reference = data.TryGetProperty("transactionRef", out var rf) ? rf.GetString() ?? "" : "";
            decimal amountNaira = data.TryGetProperty("amount", out var am) ? am.GetDecimal() : 0m;

            var tx = await _db.CafeteriaWalletTransactions
                .Include(t => t.WalletAccount)
                .FirstOrDefaultAsync(t => t.Reference == reference, ct);

            if (tx != null && !tx.Status.Equals("Successful", StringComparison.OrdinalIgnoreCase))
            {
                tx.WalletAccount.Balance += amountNaira;
                tx.WalletAccount.ConcurrencyToken = Guid.NewGuid();
                tx.WalletAccount.UpdatedAt = DateTime.UtcNow;
                tx.Status = "Successful";
                tx.BalanceAfter = tx.WalletAccount.Balance;
                tx.VerifiedAt = DateTime.UtcNow;

                try
                {
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Hydrogen webhook asynchronously credited cafeteria wallet. Ref: {Ref}, Amount: {Amount}", reference, amountNaira);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogInformation("Hydrogen webhook encountered concurrency token update for Ref: {Ref}; already verified concurrently.", reference);
                }
            }
        }
    }

    public async Task<SystemCafeteriaConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        var config = await _db.SystemCafeteriaConfigurations.FirstOrDefaultAsync(ct);
        return config ?? new SystemCafeteriaConfiguration();
    }

    public async Task EnsureAccountExistsAsync(string username, CancellationToken ct = default)
    {
        await GetOrCreateAccountAsync(username, ct);
    }

    private static DateTime GetStartOfDayWatUtc()
    {
        TimeZoneInfo watZone;
        try
        {
            watZone = TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time");
        }
        catch
        {
            try
            {
                watZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos");
            }
            catch
            {
                watZone = TimeZoneInfo.CreateCustomTimeZone("WAT", TimeSpan.FromHours(1), "West Africa Time", "WAT");
            }
        }

        var watNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, watZone);
        return TimeZoneInfo.ConvertTimeToUtc(watNow.Date, watZone);
    }

    public async Task<decimal> GetDailySpendAsync(string username, CancellationToken ct = default)
    {
        var startOfDayUtc = GetStartOfDayWatUtc();
        var cleanUsername = (username ?? string.Empty).Trim().ToLowerInvariant();

        var spend = await _db.CafeteriaWalletTransactions
            .Where(t => t.WalletAccount.Username.ToLower() == cleanUsername
                        && t.TransactionType == "Debit"
                        && t.Status == "Successful"
                        && t.CreatedAt >= startOfDayUtc)
            .SumAsync(t => (decimal?)t.Amount, ct);

        return spend ?? 0m;
    }

    public async Task<WalletDebitResult> TryDebitForMealAsync(string username, decimal amount, string description, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return new WalletDebitResult(false, 0m, "Amount must be greater than zero.", false, false);
        }

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var account = await GetOrCreateAccountAsync(username, ct);
            var config = await GetConfigurationAsync(ct);

            decimal effectiveLimit = config.DailySpendLimit;
            if (account.ParentDailySpendLimit.HasValue && account.ParentDailySpendLimit.Value > 0)
            {
                effectiveLimit = config.DailySpendLimit > 0
                    ? Math.Min(config.DailySpendLimit, account.ParentDailySpendLimit.Value)
                    : account.ParentDailySpendLimit.Value;
            }

            if (effectiveLimit > 0)
            {
                var todaysSpend = await GetDailySpendAsync(username, ct);
                if (todaysSpend + amount > effectiveLimit)
                {
                    return new WalletDebitResult(
                        Success: false,
                        account.Balance,
                        $"Daily spending limit of {effectiveLimit:N2} NGN would be exceeded. Already spent today: {todaysSpend:N2}.",
                        DailyLimitExceeded: true,
                        InsufficientFunds: false);
                }
            }

            if (account.Balance < amount)
            {
                return new WalletDebitResult(
                    Success: false,
                    account.Balance,
                    $"Insufficient wallet balance. Available: {account.Balance:N2}, required: {amount:N2}.",
                    DailyLimitExceeded: false,
                    InsufficientFunds: true);
            }

            account.Balance -= amount;
            account.ConcurrencyToken = Guid.NewGuid();
            account.UpdatedAt = DateTime.UtcNow;

            var tx = new CafeteriaWalletTransaction
            {
                WalletAccountId = account.Id,
                Amount = amount,
                TransactionType = "Debit",
                Gateway = "CafeteriaWallet",
                Reference = $"WAL-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
                Status = "Successful",
                Description = description ?? "Meal Purchase",
                BalanceAfter = account.Balance,
                CreatedAt = DateTime.UtcNow,
                VerifiedAt = DateTime.UtcNow
            };

            _db.CafeteriaWalletTransactions.Add(tx);

            try
            {
                await _db.SaveChangesAsync(ct);
                return new WalletDebitResult(true, account.Balance, "Wallet debited successfully for meal.", false, false);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Concurrency conflict during meal debit for {Username}, attempt {Attempt} of {MaxRetries}. Retrying...", username, attempt, maxRetries);
                _db.ChangeTracker.Clear();
                await Task.Delay(50 * attempt, ct);
            }
        }

        return new WalletDebitResult(false, 0m, "Transaction could not be completed due to high concurrency. Please try again.", false, false);
    }

    public async Task<WalletDebitResult> RefundMealOrderAsync(Guid orderId, string reason, CancellationToken ct = default)
    {
        var order = await _db.CafeteriaVendorOrders.FindAsync([orderId], ct);
        if (order is null)
        {
            return new WalletDebitResult(false, 0m, "Order not found.", false, false);
        }

        if (order.IsScholarshipCovered || order.Price <= 0)
        {
            return new WalletDebitResult(true, 0m, "Order was scholarship-covered or zero-cost; no wallet refund required.", false, false);
        }

        string cleanUsername = order.StudentUsername.Trim().ToLowerInvariant();
        var account = await GetOrCreateAccountAsync(cleanUsername, ct);

        // Idempotent: prevent double refund if already processed
        string refundRefPrefix = $"REF-{order.OrderCode}";
        var existingRefund = await _db.CafeteriaWalletTransactions
            .AnyAsync(t => t.WalletAccountId == account.Id 
                        && t.TransactionType == "Refund" 
                        && t.Reference.StartsWith(refundRefPrefix), ct);

        if (existingRefund)
        {
            return new WalletDebitResult(true, account.Balance, "Order has already been refunded.", false, false);
        }

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            account = await GetOrCreateAccountAsync(cleanUsername, ct);
            account.Balance += order.Price;
            account.ConcurrencyToken = Guid.NewGuid();
            account.UpdatedAt = DateTime.UtcNow;

            var tx = new CafeteriaWalletTransaction
            {
                WalletAccountId = account.Id,
                Amount = order.Price,
                TransactionType = "Refund",
                Gateway = "CafeteriaWallet",
                Reference = $"REF-{order.OrderCode}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Status = "Successful",
                Description = string.IsNullOrWhiteSpace(reason)
                    ? $"Refund for cancelled meal: {order.MenuItemName} ({order.OrderCode})"
                    : $"Refund for order {order.OrderCode}: {reason}",
                BalanceAfter = account.Balance,
                CreatedAt = DateTime.UtcNow,
                VerifiedAt = DateTime.UtcNow
            };

            _db.CafeteriaWalletTransactions.Add(tx);

            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Successfully refunded {Amount:N2} NGN to {Username} for cancelled order {OrderCode}.", order.Price, order.StudentUsername, order.OrderCode);
                return new WalletDebitResult(true, account.Balance, "Wallet refunded successfully.", false, false);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Concurrency conflict during refund for {Username}, attempt {Attempt} of {MaxRetries}. Retrying...", cleanUsername, attempt, maxRetries);
                _db.ChangeTracker.Clear();
                await Task.Delay(50 * attempt, ct);
            }
        }

        return new WalletDebitResult(false, account.Balance, "Refund could not be completed due to high concurrency. Please retry.", false, false);
    }

    public async Task<PayWithWalletResponse> PayWithWalletAsync(PayWithWalletRequest req, CancellationToken ct = default)
    {
        if (req.Amount <= 0)
        {
            return new PayWithWalletResponse(false, "Amount must be greater than zero.", 0m);
        }

        var result = await TryDebitForMealAsync(req.Username, req.Amount, req.Description ?? "Meal Purchase", ct);
        if (!result.Success)
        {
            return new PayWithWalletResponse(false, result.Message, result.NewBalance);
        }

        return new PayWithWalletResponse(true, "Wallet payment successful.", result.NewBalance);
    }
}
