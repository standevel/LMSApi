using System;

namespace LMS.Api.Data.Entities;

public sealed class CafeteriaWalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletAccountId { get; set; }
    public CafeteriaWalletAccount WalletAccount { get; set; } = null!;

    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = "Credit"; // Credit or Debit
    public string Gateway { get; set; } = "Paystack"; // Paystack, Hydrogen, Direct, MealPayment
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Successful, Failed
    public string Description { get; set; } = string.Empty;
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
}
