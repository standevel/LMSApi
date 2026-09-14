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

        await _db.SaveChangesAsync(ct);

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
                tx.WalletAccount.UpdatedAt = DateTime.UtcNow;
                tx.Status = "Successful";
                tx.BalanceAfter = tx.WalletAccount.Balance;
                tx.VerifiedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Webhook asynchronously credited cafeteria wallet. Ref: {Ref}, Amount: {Amount}", reference, amountNaira);
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
                tx.WalletAccount.UpdatedAt = DateTime.UtcNow;
                tx.Status = "Successful";
                tx.BalanceAfter = tx.WalletAccount.Balance;
                tx.VerifiedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Hydrogen webhook asynchronously credited cafeteria wallet. Ref: {Ref}, Amount: {Amount}", reference, amountNaira);
            }
        }
    }
}
